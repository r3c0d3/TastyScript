﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TastyScript.Lang.Extensions;
using TastyScript.Lang.Tokens;

namespace TastyScript.Lang.Functions
{
    [Function("Loop", new string[] { "invoke" }, isSealed: true, invoking: true, isanon: false)]
    internal class FunctionLoop : FDefinition
    {
        public override string CallBase()
        {
            this.IsLoop = true;
            var prov = ProvidedArgs.First("invoke");
            if (prov == null)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"[247]Invoke function cannot be null.", LineValue));
                return null;
            }
            var func = FunctionStack.First(prov.ToString());
            if (func == null)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"[250]Invoke function cannot be null.", LineValue));
                return null;
            }
            var findFor = Extensions.FirstOrDefault(f => f.Name == "For") as ExtensionFor;
            if (findFor != null && findFor.Extend() != null && findFor.Extend().ElementAtOrDefault(0) != null && findFor.Extend()[0] != "")
            {
                string[] forNumber = findFor.Extend();
                int forNumberAsNumber = int.Parse(forNumber[0].ToString());
                //if (forNumberAsNumber == 0)
                //    forNumberAsNumber = int.MaxValue;
                var tracer = new LoopTracer();
                Compiler.LoopTracerStack.Add(tracer);
                for (var x = 0; x <= forNumberAsNumber; x++)
                {
                    //gave a string as the parameter because number was causing srs problems
                    if (!TokenParser.Stop)
                    {
                        if (tracer.Break)
                        {
                            break;
                        }
                        if (tracer.Continue)
                        {
                            tracer.SetContinue(false);//reset continue
                        }
                        var passed = this.GetInvokeProperties();
                        if (passed != null)
                        {
                            var getFirstElement = passed.ElementAtOrDefault(0);
                            if (getFirstElement != null)
                            {
                                passed[0] = x.ToString();
                            }
                            else
                            {
                                passed = new string[] { x.ToString() };
                            }
                        }
                        else
                        {
                            passed = new string[] { x.ToString() };
                        }
                        func.SetInvokeProperties(new string[] { }, Caller.CallingFunction.LocalVariables.List, Caller.CallingFunction.ProvidedArgs.List);
                        func.TryParse(new TFunction(Caller.Function, new List<EDefinition>(), passed, this, tracer));
                    }
                    else
                    {
                        break;
                    }
                }
                Compiler.LoopTracerStack.Remove(tracer);
                tracer = null;
            }
            else
            {
                //LoopTracer tracer = new LoopTracer();
                //Compiler.LoopTracerStack.Add(tracer);
                //Tracer = tracer;
                var tracer = new LoopTracer();
                Compiler.LoopTracerStack.Add(tracer);
                var x = 0;
                while (true)
                {
                    //gave a string as the parameter because number was causing srs problems
                    if (!TokenParser.Stop)
                    {
                        if (tracer.Break)
                        {
                            break;
                        }
                        if (tracer.Continue)
                        {
                            tracer.SetContinue(false);//reset continue
                        }
                        var passed = this.GetInvokeProperties();
                        if (passed != null)
                        {
                            var getFirstElement = passed.ElementAtOrDefault(0);
                            if (getFirstElement != null)
                            {
                                passed[0] = x.ToString();
                            }
                            else
                            {
                                passed = new string[] { x.ToString() };
                            }
                        }
                        else
                        {
                            passed = new string[] { x.ToString() };
                        }
                        //Console.WriteLine(func.UID+JsonConvert.SerializeObject(tracer, Formatting.Indented));
                        func.SetInvokeProperties(new string[] { }, Caller.CallingFunction.LocalVariables.List, Caller.CallingFunction.ProvidedArgs.List);
                        func.TryParse(new TFunction(Caller.Function, new List<EDefinition>(), passed, this, tracer));
                        x++;
                        //Console.WriteLine("\t\t\t" +func.UID + ":" + string.Join(",",func.ProvidedArgs));
                        //Console.WriteLine(func.UID + JsonConvert.SerializeObject(func.ProvidedArgs, Formatting.Indented));
                    }
                    else
                    {
                        break;
                    }
                }
                //foreach (var ttt in Compiler.LoopTracerStack)
                //    Console.WriteLine("b"+func.UID + JsonConvert.SerializeObject(ttt, Formatting.Indented));
                Compiler.LoopTracerStack.Remove(tracer);
                //foreach(var ttt in Compiler.LoopTracerStack)
                //    Console.WriteLine("a"+func.UID + JsonConvert.SerializeObject(ttt, Formatting.Indented));
                tracer = null;
            }
            return "";
        }
        //stop the base for looping extension from overriding this custom looping function
        protected override void ForExtension(TFunction caller, ExtensionFor findFor)
        {
            TryParse(caller, true);
        }
    }
}
