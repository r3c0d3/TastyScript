﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using TastyScript.Lang.Extensions;
using TastyScript.Lang.TokenOLD;

namespace TastyScript.Lang.Tokens
{
    [Serializable]
    internal class Token
    {
        public string Name { get; protected set; }
        private string _value;
        public string Value
        {
            get
            {
                if (_action != null)
                    return _action();
                return _value;
            }
        }
        public List<EDefinition> Extensions { get; set; }
        public string Line { get; protected set; }
        public bool Locked { get; protected set; }
        protected Func<string> _action;
        /// <summary>
        /// line param is the line reference for the exception handler to track down
        /// </summary>
        /// <param name="name"></param>
        /// <param name="val"></param>
        /// <param name="line"></param>
        public Token() { }
        public Token(string name, string val, string line, bool locked = false)
        {
            Name = name;
            _value = val;
            Line = line;
            Locked = locked;
        }
        public Token(string name, Func<string> action, string line, bool locked = false)
        {
            Name = name;
            _action = action;
            _value = "__Action";
            Line = line;
            Locked = locked;
        }
        public void SetValue(string value)
        {
            _value = value;
        }
        public void SetLine(string line)
        {
            Line = line;
        }
        public string[] ToArray()
        {
            var commaRegex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            return commaRegex.Split(Value);
        }
        public override string ToString()
        {
            //if (_value.Contains("[") && _value.Contains("]"))
            //    return _value.Substring(1, _value.Length - 2);
            //else
                return _value.Replace("[","").Replace("]","");
        }
    }
    internal class TFunction : Token
    {
        public IBaseFunction Function { get; private set; }
        public string Arguments { get; private set; }
        public bool BlindExecute { get; set; }
        public LoopTracer Tracer { get; }
        public TFunction(IBaseFunction func, List<EDefinition> ext, string args)
        {
            Name = func.Name;
            Function = func;
            Extensions = ext;
            Arguments = args;
        }
        public TFunction(IBaseFunction func, List<EDefinition> ext, string[] args)
        {
            Name = func.Name;
            Function = func;
            Extensions = ext;
            Arguments = ArrayToString(args);
        }
        private string ArrayToString(string[] arr)
        {
            return string.Join(",", arr);
        }
        [Obsolete]
        public string[] ReturnArgsArray()
        {
            //Console.WriteLine(Arguments);
            var output = Arguments;
            var index = 0;
            Dictionary<string, string> stringParts = new Dictionary<string, string>();
            var stringRegex = new Regex("\"([^\"\"]*)\"", RegexOptions.Multiline);
            foreach(var s in stringRegex.Matches(output))
            {
                var token = "{AutoGeneratedToken" + index + "}";
                stringParts.Add(token, s.ToString());
                output = output.Replace(s.ToString(), token);
                index++;
            }
            Dictionary<string, string> parenParts = new Dictionary<string, string>();
            var reg = new Regex(@"(?:(?:\[(?>[^\[\]]+|\[(?<number>)|\](?<-number>))*(?(number)(?!))\])|[^[]])+");
            foreach (var x in reg.Matches(output))
            {
                var token = "{AutoGeneratedToken" + index + "}";
                parenParts.Add(token, x.ToString());
                output = output.Replace(x.ToString(), token);
                index++;
            }
            var splode = output.Split(',');
            //List<string> returnParens = new List<string>();
            
            for (var i = 0; i < splode.Length; i++) 
            {
                foreach(var p in parenParts)
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
                        splode[i] = splode[i].Replace(p.Key, p.Value).Replace("\"","");
                }
            }
            //foreach (var x in splode)
             //   Console.WriteLine("   " + x);
            return splode;
        }
    }
    //
    //OLD VVVV
    //
    [Serializable]
    internal abstract class Token<T> : IToken<T>
    {
        protected abstract string _name { get; set; }
        public string Name { get { return _name; } }
        protected abstract BaseValue<T> _value { get; set; }
        public virtual BaseValue<T> Value { get { return _value; } }
        public TParameter Arguments { get; set; }
        public List<EDefinition> Extensions { get; set; }
        public bool Locked { get; protected set; }
        public override string ToString()
        {
            return Value.ToString();
        }
        public object GetValue()
        {
            return Value.Value;
        }
    }
    internal interface IBaseToken
    {
        bool Locked { get; }
        string Name { get; }
        //TParameter Arguments { get; set; }
        object GetValue();
        List<EDefinition> Extensions { get; set; }
    }

    internal interface IToken<T> : IBaseToken
    {
        BaseValue<T> Value { get; }
    }

    internal class IBaseValue { }
    [Serializable]
    internal class BaseValue<T> : IBaseValue
    {
        public T Value { get; private set; }

        public BaseValue(T val)
        {
            Value = val;
        }
        public void SetValue(T val)
        {
            Value = val;
        }
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
