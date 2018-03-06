﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TastyScript.Lang.Extensions;
using TastyScript.Lang.Tokens;

namespace TastyScript.Lang
{
    internal class Line
    {
        private IBaseFunction _reference;
        public string Value { get; private set; }
        public TFunction Token { get; private set; }

        public Line(string val, IBaseFunction reference)
        {
            Compiler.ExceptionListener.SetCurrentLine(val);
            Value = val;
            _reference = reference;
            Token = WalkTree(val);
        }

        private TFunction WalkTree(string value)
        {
            value = value.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            TFunction temp = null;
            value = ParseMathExpressions(value);
            value = ParseArrays(value);
            value = ParseStrings(value);
            value = ParseNumbers(value);
            value = value.Replace(".", "<-").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            //check for empty lines
            var wscheck = new Regex(@"^\s*$");
            var wscheckk = wscheck.IsMatch(value);
            if (wscheckk)
                return temp;
            //
            //get var extensions before normal extensions
            value = EvaluateVarExtensions(value);
            var ext = ParseExtensions(value);
            //vars here
            if (value.Contains("var "))
            {
                value = EvaluateVar(value);
                if (value == "")
                    return temp;
            }
            //
            temp = ParseFunctions(value, ext);
            return temp;
        }
        private string ParseStrings(string value)
        {
            var stringTokenRegex = new Regex("\"([^\"\"]*)\"", RegexOptions.Multiline);
            var strings = stringTokenRegex.Matches(value);
            foreach (var x in strings)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                var tstring = new Token(tokenname, Regex.Replace(x.ToString(), "\"", ""),Value);
                value = value.Replace(x.ToString(), tokenname);

                TokenParser.AnonymousTokens.Add(tstring);
            }
            return value;
        }
        private string ParseNumbers(string value)
        {
            var numberTokenRegex = new Regex(@"\b-*[0-9\.]+\b", RegexOptions.Multiline);
            var numbers = numberTokenRegex.Matches(value);
            foreach (var x in numbers)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                double output = 0;
                var nofail = double.TryParse(x.ToString(), out output);
                if (nofail)
                {
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, output.ToString(),Value));
                    //do this regex instead of a blind replace to fix the above issue. 
                    //NOTE this fix may break decimal use in some situations!!!!
                    var indvRegex = (@"\b-*" + x + @"\b");
                    var regex = new Regex(indvRegex);
                    value = regex.Replace(value, tokenname);
                }
            }
            return value;
        }
        private string ParseMathExpressions(string value)
        {
            var mathexpRegex = new Regex(@"\[([^\[\]]*)\]", RegexOptions.Multiline);
            var mathexp = mathexpRegex.Matches(value);
            foreach (var x in mathexp)
            {
                var input = x.ToString().Replace("[", "").Replace("]", "").Replace(" ", "");
                if (input != null && input != "")
                {
                    string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                    double exp = MathExpression(input);
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, exp.ToString(), Value));
                    value = value.Replace(x.ToString(), tokenname);
                }
            }
            return value;
        }
        private string ParseArrays(string value)
        {
            string val = value;
            //first we have to find all the arrays
            var arrayRegex = new Regex(@"\(([^()]*)\)", RegexOptions.Multiline);
            var arrayMatches = arrayRegex.Matches(val);
            foreach (var a in arrayMatches)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                var param = a.ToString().Replace("(","").Replace(")","");
                var compCheck = ComparisonCheck(param);
                if (compCheck != "")
                {
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, compCheck, val));
                }
                else
                {
                    var commaRegex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                    var commaSplit = commaRegex.Split(param);
                    var tokens = GetTokens(commaSplit,true);
                    //make sure values are being collected and not tokens
                    if (tokens.Count > 0)
                    {
                        for (int i = 0; i < commaSplit.Length; i++) 
                        {
                            var obj = tokens.FirstOrDefault(f => f.Name == commaSplit[i]);
                            if (obj != null)
                                commaSplit[i] = obj.Value;
                        }
                        param = string.Join(",", commaSplit);
                    }
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, $"[{param}]", val));
                }
                val = val.Replace(a.ToString(), "->" + tokenname + "|");
            }
            return val;
        }
        private List<EDefinition> ParseExtensions(string value)
        {
            List<EDefinition> temp = new List<EDefinition>();
            if (value.Contains("<-"))
            {
                string val = value;
                var firstSplit = val.Split(new string[] { "<-" }, StringSplitOptions.None);
                for (int i = 0; i < firstSplit.Length; i++) 
                {
                    if (i == 0)
                        continue;
                    var first = firstSplit[i];
                    var secondSplit = first.Split(new string[] { "->" }, StringSplitOptions.None);
                    if (secondSplit.Length != 2)
                        Compiler.ExceptionListener.Throw("[160]Extensions must provide arguments",ExceptionType.SyntaxException);
                    var original = TokenParser.Extensions.FirstOrDefault(f => f.Name == secondSplit[0]);
                    //Console.WriteLine(secondSplit[0] + " " + secondSplit[1]);
                    var clone = DeepCopy<EDefinition>(original);
                    var param = GetTokens(new string[] { secondSplit[1].Replace("|", "") });
                    if (param.Count != 1)
                        Compiler.ExceptionListener.Throw("[166]Extensions must provide arguments", ExceptionType.SyntaxException);
                    clone.SetArguments(param[0].ToString());
                    temp.Add(clone);
                }
            }
            return temp;
        }
        private TFunction ParseFunctions(string value, List<EDefinition> ext)
        {
            TFunction temp = null;

            var firstSplit = value.Split('|')[0];
            var secondSplit = firstSplit.Split(new string[] { "->" }, StringSplitOptions.None);
            var func = TokenParser.FunctionList.FirstOrDefault(f => f.Name == secondSplit[0]);
            if (func == null)
                Compiler.ExceptionListener.Throw($"[181]Cannot find function [{secondSplit[0]}]", ExceptionType.SyntaxException);
            //get args
            var param = GetTokens(new string[] { secondSplit[1] });
            if (param.Count != 1)
                Compiler.ExceptionListener.Throw("[185]Extensions must provide arguments", ExceptionType.SyntaxException);
            var returnObj = new TFunction(func, ext, param[0].ToString());
            temp = returnObj;
            return temp;
        }

        private List<Token> GetTokens(string[] names, bool safe = false, bool returnInput = false)
        {
            List<Token> temp = new List<Token>();
            foreach (var n in names)
            {
                var stripws = n.Replace(" ", "");
                var tryLocal = _reference.LocalVariables.FirstOrDefault(f => f.Name == stripws);
                if (tryLocal != null)
                {
                    temp.Add(new Token(stripws, tryLocal.Value,Value));
                    continue;
                }
                var tryGlobal = TokenParser.GlobalVariables.FirstOrDefault(f => f.Name == stripws);
                if (tryGlobal != null)
                {
                    temp.Add(new Token(stripws, tryGlobal.Value, Value));
                    continue;
                }
                var tryAnon = TokenParser.AnonymousTokens.FirstOrDefault(f => f.Name == stripws);
                if (tryAnon != null)
                {
                    temp.Add(new Token(stripws, tryAnon.Value, Value));
                    continue;
                }
                //try params?
                var tryParams = _reference.ProvidedArgs.FirstOrDefault(f => f.Name == stripws);
                if(tryParams != null)
                {
                    temp.Add(new Token(stripws, tryParams.Value, Value));
                    continue;
                }
                if (returnInput)
                {
                    temp.Add(new Token(stripws, stripws, Value));
                }
            }

            if (temp.Count == 0 && !safe)
                Compiler.ExceptionListener.Throw($"Cannot find tokens [{string.Join(",",names)}]");
            return temp;
        }
        private double MathExpression(string expression)
        {
            string exp = expression;
            //get vars and params out of the expression
            
            var varRegex = new Regex(@"\w[A-Za-z]*\d*");
            var varRegexMatches = varRegex.Matches(exp);
            foreach (var x in varRegexMatches)
            {
                var tok = GetTokens(new string[] { x.ToString() },true);

                var tokfirst = tok.FirstOrDefault(f => f != null);
                if (tokfirst != null)
                {
                    exp = exp.Replace(x.ToString(), tokfirst.ToString());
                }
            }
            try
            {
                DataTable table = new DataTable();
                table.Columns.Add("expression", typeof(string), exp);
                DataRow row = table.NewRow();
                table.Rows.Add(row);
                return double.Parse((string)row["expression"]);
            }
            catch (Exception e)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[331]Unexpected error with mathematical expression:\n{e.Message}",
                    Value));
            }
            return 0;
        }
        #region Comparison
        enum Operator { EQ, NOTEQ, GT, LT, GTEQ, LTEQ }
        private string ComparisonCheck(string line)
        {
            string output = "";
            if (line.Contains("=="))
                output = FindOperation(Operator.EQ, line);
            else if (line.Contains("!="))
                output = FindOperation(Operator.NOTEQ, line);
            else if (line.Contains(">="))
                output = FindOperation(Operator.GTEQ, line);
            else if (line.Contains("<="))
                output = FindOperation(Operator.LTEQ, line);
            else if (line.Contains(">"))
                output = FindOperation(Operator.GT, line);
            else if (line.Contains("<"))
                output = FindOperation(Operator.LT, line);
            return output;
        }
        //the heavy lifting for comparison check
        private string FindOperation(Operator op, string line)
        {
            string output = "";
            string opString = "";
            switch (op)
            {
                case (Operator.EQ):
                    opString = "==";
                    break;
                case (Operator.NOTEQ):
                    opString = "!=";
                    break;
                case (Operator.GT):
                    opString = ">";
                    break;
                case (Operator.LT):
                    opString = "<";
                    break;
                case (Operator.GTEQ):
                    opString = ">=";
                    break;
                case (Operator.LTEQ):
                    opString = "<=";
                    break;
            }
            var splitop = line.Split(new string[] { opString }, StringSplitOptions.None);
            var lr = GetTokens(new string[] { splitop[0], splitop[1] },true,true);
            if (lr.Count != 2)
                Compiler.ExceptionListener.Throw("There must be one left-hand and one right-hand in comparison objects.",
                    ExceptionType.SyntaxException);
            try
            {
                switch (op)
                {
                    case (Operator.EQ):
                        output = (lr[0].ToString() == lr[1].ToString())
                            ? "True" : "False";
                        break;
                    case (Operator.NOTEQ):
                        output = (lr[0].ToString() != lr[1].ToString())
                            ? "True" : "False";
                        break;
                    case (Operator.GT):
                        output = (double.Parse(lr[0].ToString()) > double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.LT):
                        output = (double.Parse(lr[0].ToString()) < double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.GTEQ):
                        output = (double.Parse(lr[0].ToString()) >= double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.LTEQ):
                        output = (double.Parse(lr[0].ToString()) <= double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException, $"Unexpected input: {line}"));
            }

            return output;
        }
        //this rips off the comparison check, since the concept is the same.
        private void CompareFail(string line)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException, $"Can not compare more or less than 2 values", line));
        }
        #endregion
        public static T DeepCopy<T>(T obj)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new Exception("The source object must be serializable");
            }

            if (Object.ReferenceEquals(obj, null))
            {
                throw new Exception("The source object must not be null");
            }
            T result = default(T);
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, obj);
                memoryStream.Seek(0, SeekOrigin.Begin);
                result = (T)formatter.Deserialize(memoryStream);
                memoryStream.Close();
            }
            return result;

        }

        private string EvaluateVarExtensions(string val)
        {
            var value = val;
            if (val.Contains("<-"))
            {
                //get extensions
                var ext = ParseExtensions(value);
                //get object to be extended
                var strip = value.Split(new string[] { "<-" },StringSplitOptions.None);
                var objLeft = strip[0];
                var objRemoveKeywords = objLeft.Split(new string[] { "+=", "-=", "++", "--", "=" }, StringSplitOptions.RemoveEmptyEntries);
                var obj = objRemoveKeywords[objRemoveKeywords.Length - 1];
                var objVar = GetTokens(new string[] { obj.Replace("|","") }, true).FirstOrDefault();
                if (objVar != null)
                {
                    foreach (var e in ext)
                    {
                        if (e is ExtensionGetItem)
                        {
                            string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                            var thisExt = e as ExtensionGetItem;
                            TokenParser.AnonymousTokens.Add(
                                new Token(tokenname, thisExt.Extend(objVar)[0], Value));
                            //replace the old token with the new token, and remove the extension
                            value = value.Replace(obj + "<-" + strip[1], tokenname);
                            //value = value.Replace(obj, tokenname);
                            //value = value.Replace("." + strip[1], "");
                        }
                        if (e is ExtensionSetItem)
                        {
                            string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                            var thisExt = e as ExtensionSetItem;
                            TokenParser.AnonymousTokens.Add(
                                new Token(tokenname, string.Join(",", thisExt.Extend(objVar)), Value));
                            //replace the old token with the new token, and remove the extension
                            value = value.Replace(obj + "<-" + strip[1], tokenname);
                            //if self-assigning ommitting left hand
                            if (!value.Contains("="))
                            {
                                //creates the assignment line to compensate from left hand ommission
                                value = $"var {obj}={tokenname}";
                            }
                        }
                        if (e is ExtensionGetIndex)
                        {
                            string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                            var thisExt = e as ExtensionGetIndex;
                            TokenParser.AnonymousTokens.Add(
                                new Token(tokenname, thisExt.Extend(objVar)[0], Value));
                            //replace the old token with the new token, and remove the extension
                            value = value.Replace(obj + "<-" + strip[1], tokenname);
                        }
                    }
                }
            }
            return value;
        }
        private string EvaluateVar(string value)
        {
            //get the var scope
            List<Token> varList = null;
            if (value.Contains("$var "))
                varList = TokenParser.GlobalVariables;
            else if (value.Contains("var "))
                varList = _reference.LocalVariables;
            if (varList == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[244]Unexpected error occured.",Value));
            //assign based on operator

            var strip = value.Replace("$", "").Replace("var ", "");
            string[] assign = default(string[]);
            if (strip.Contains("++"))
                assign = strip.Split(new string[] { "++" }, StringSplitOptions.None);
            else if (strip.Contains("--"))
                assign = strip.Split(new string[] { "--" }, StringSplitOptions.None);
            else if (strip.Contains("+="))
                assign = strip.Split(new string[] { "+=" }, StringSplitOptions.None);
            else if (strip.Contains("-="))
                assign = strip.Split(new string[] { "-=" }, StringSplitOptions.None);
            else if (strip.Contains("="))
                assign = strip.Split(new string[] { "=" }, StringSplitOptions.None);

            //get the left hand
            var leftHand = assign[0].Replace(" ", "");
            var varRef = varList.FirstOrDefault(f => f.Name == leftHand);

            //one sided assignment
            if (strip.Contains("++") || strip.Contains("--"))
            {
                if (varRef == null)
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[269]Cannot find the left hand variable.", Value));
                double numOut = 0;
                double.TryParse(varRef.ToString(), out numOut);
                if (strip.Contains("++"))
                    numOut++;
                else
                    numOut--;
                varRef.SetValue(numOut.ToString());
                return "";
            }
            var rightHand = assign[1].Replace(" ", "");
            if (rightHand.Contains("<-"))
            {
                //rightHand = EvaluateVarExtensions(rightHand);
            }
            rightHand = rightHand.Replace("->", "").Replace("|","");//just in case
            if (varRef != null && varRef.Locked)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[282]Cannot re-assign a sealed variable!", Value));
            if (rightHand == null || rightHand == "" || rightHand == " ")
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[285]Right hand must be a value.", Value));
            Token token = GetTokens(new string[] { rightHand }).ElementAtOrDefault(0);
            if (token == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[285]Right hand must be a value.", Value));

            if (strip.Contains("+=") || strip.Contains("-="))
            {
                if (varRef == null)
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[291]Cannot find the left hand variable.", Value));
                //check if number and apply the change
                double leftNumOut = 0;
                double rightNumOut = 0;

                //check if token is a number too
                var nofailRight = double.TryParse(token.ToString(), out rightNumOut);
                var nofailLeft = double.TryParse(varRef.ToString(), out leftNumOut);
                if (nofailLeft && nofailRight)
                {
                    if (strip.Contains("+="))
                        leftNumOut += rightNumOut;
                    else
                        leftNumOut -= rightNumOut;
                    varRef.SetValue(leftNumOut.ToString());
                }
                else//one or both arent numbers, which means concatenation intead of incrementation.
                {
                    var str = varRef.ToString();
                    if (strip.Contains("+="))
                        str += token.ToString();
                    else
                        Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                            "[314]Cannot apply the operand -= with type string.", Value));
                    varRef.SetValue(str);
                }
                return "";
            }
            if (strip.Contains("="))
            {
                if (varRef != null)
                {
                    varRef.SetValue(token.ToString());
                }
                else
                    varList.Add(new Token(leftHand, token.ToString(),Value));
                return "";
            }
            Compiler.ExceptionListener.Throw("[330]Unknown error with assignment.", ExceptionType.SyntaxException, Value);
            return "";
        }
    }
}
