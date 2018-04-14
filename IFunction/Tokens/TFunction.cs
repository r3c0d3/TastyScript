﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TastyScript.IFunction.Containers;
using TastyScript.IFunction.Extension;
using TastyScript.IFunction.Function;
using TastyScript.ParserManager.Looping;

namespace TastyScript.IFunction.Tokens
{
    public class TFunction : Token
    {
        private LoopTracer Tracer;
        public BaseFunction ParentFunction { get; }
        private BaseFunction Function;
        private string[] InvokeProperties;
        public string[] Arguments { get; }
        private Dictionary<string, object> DynamicDictionary;

        public TFunction(BaseFunction function)
        {
            InvokeProperties = new string[] { };
            Function = function;
            Name = function.Name;
            DynamicDictionary = ParentFunction?.Caller.DynamicDictionary;
            Extensions = new ExtensionList();
        }
        public TFunction(BaseFunction function, BaseFunction parentFunction)
            : this(function)
        {
            ParentFunction = parentFunction;
        }
        public TFunction(BaseFunction function, BaseFunction parentFunction, ExtensionList extensions)
            : this(function, parentFunction)
        {
            Extensions = extensions;
        }
        public TFunction(BaseFunction function, BaseFunction parentFunction, ExtensionList extensions, string[] arguments)
            : this(function, parentFunction, extensions)
        {
            Arguments = arguments;
        }
        public TFunction(BaseFunction function, BaseFunction parentFunction, ExtensionList extensions, string arguments)
            : this(function, parentFunction, extensions)
        {
            Arguments = GetArgsArray(arguments);
        }
        public TFunction(BaseFunction function, string arguments)
            : this(function, null, new ExtensionList(), arguments) { }
        public TFunction(BaseFunction function, string[] arguments)
            : this(function, null, new ExtensionList(), arguments) { }
        public TFunction(BaseFunction function, BaseFunction parentFunction, string[] arguments)
            : this(function, parentFunction, new ExtensionList(), arguments){ }
        public TFunction(BaseFunction function, BaseFunction parentFunction, string arguments)
            : this(function, parentFunction, new ExtensionList(), arguments) { }


        // MAYBE Use tryparse here instead on the function iteslf??
        public Token TryParse()
        {
            Function.TryParse(Inherit());
            return Function.ReturnBubble;
        }
        public Token TryParse(bool forflag)
        {
            Function.TryParse(Inherit(), forflag);
            return Function.ReturnBubble;
        }

        public void SetInvokeProperties(string[] props) => InvokeProperties = props;
        //caller.callingfunction.extensions
        public ExtensionList GetParentExtension() => ParentFunction?.Extensions;
        //caller.callingfunction.caller.dynamicdictionary
        public Dictionary<string, object> GetParentDynamicDictionary() => ParentFunction?.Caller?.DynamicDictionary;
        //caller.callingfunction.localvariables
        public TokenList GetParentLocalVariables() => ParentFunction?.LocalVariables;
        //caller.callingfunction.providedargs
        public TokenList GetParentLocalArguments() => ParentFunction?.ProvidedArgs;
        //caller.callingfunction.caller.callingfunction.providedargs
        public TokenList GetParentOfParentLocalArguments() => ParentFunction?.Caller.ParentFunction?.ProvidedArgs;
        //caller.callingfunction.caller.callingfunction.localvariables
        public TokenList GetParentOfParentLocalVariables() => ParentFunction?.Caller.ParentFunction?.LocalVariables;
        //caller.callingfunction.base
        public BaseFunction GetParentBase() => ParentFunction?.Base;
        //caller.callingfunction.returntotopofbubble
        public void SetParentReturnToTopOfBubble(Token value) => ParentFunction?.ReturnToTopOfBubble(value);
        //caller.callingfunction.isloop
        public bool IsParentLoop() => IsParentNull() ? false : ParentFunction.IsLoop;
        //caller.callingfunction is null
        public bool IsParentNull() => ParentFunction == null ? true : false;
        //caller.callingfunction.isinvoking
        public bool IsParentInvoking() => IsParentNull() ? false : ParentFunction.IsInvoking;

        public bool IsParentBlindExecute() => IsParentNull() ? false : ParentFunction.IsBlindExecute;
        

        /// <summary>
        /// This redirect is when the called function is `Base` and needs to be redirected
        /// to the base of the parent function
        /// </summary>
        /// <param name="b"></param>
        public void RedirectFunctionToParentBase() => Function = ParentFunction.Base;

        public void RedirectFunctionToNewFunction(BaseFunction func) => Function = func;

        public void SetDynamicDictionary(Dictionary<string, object> dyn) => DynamicDictionary = dyn;

        public CallerInheritObject Inherit() => new CallerInheritObject(this, Tracer, Extensions, InvokeProperties);

        public void SetTracer(LoopTracer tracer) => Tracer = tracer;
        
        private string[] GetArgsArray(string args)
        {
            if (args == null)
                return new string[] { };
            var output = args;
            var index = 0;
            Dictionary<string, string> stringParts = new Dictionary<string, string>();
            Dictionary<string, string> parenParts = new Dictionary<string, string>();
            if (args.Contains("\""))
            {
                var stringRegex = new Regex("\"([^\"\"]*)\"", RegexOptions.Multiline);
                foreach (var s in stringRegex.Matches(output))
                {
                    var token = "{AutoGeneratedToken" + index + "}";
                    stringParts.Add(token, s.ToString());
                    output = output.Replace(s.ToString(), token);
                    index++;
                }
            }
            if (args.Contains("[") && args.Contains("]"))
            {
                var reg = new Regex(@"(?:(?:\[(?>[^\[\]]+|\[(?<number>)|\](?<-number>))*(?(number)(?!))\])|[^[]])+");
                foreach (var x in reg.Matches(output))
                {
                    var token = "{AutoGeneratedToken" + index + "}";
                    parenParts.Add(token, x.ToString());
                    output = output.Replace(x.ToString(), token);
                    index++;
                }
            }
            var splode = output.Split(',');

            for (var i = 0; i < splode.Length; i++)
            {
                foreach (var p in parenParts)
                {
                    if (splode[i].Contains(p.Key))
                        splode[i] = splode[i].Replace(p.Key, p.Value);
                }
            }
            for (var i = 0; i < splode.Length; i++)
            {
                foreach (var p in stringParts)
                {
                    if (splode[i].Contains(p.Key))
                        splode[i] = splode[i].Replace(p.Key, p.Value).Replace("\"", "");
                }
            }
            return splode;
        }
    }
    //a temp object used for passing relevent information to 
    //the called function. lets try this instead of making all TFunction properties public
    public class CallerInheritObject
    {
        public TFunction Caller;
        public LoopTracer Tracer;
        public ExtensionList Extensions;
        public string[] InvokeProperties;
        public CallerInheritObject(TFunction caller, LoopTracer tracer, ExtensionList extensions, string[] invokeProperties)
        {
            Caller = caller;
            Tracer = tracer;
            Extensions = extensions;
            InvokeProperties = invokeProperties;
        }
    }
}
