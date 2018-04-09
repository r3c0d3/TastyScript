﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TastyScript.IFunction.Extensions;
using TastyScript.IFunction.Tokens;

namespace TastyScript.IFunction.Functions.Internal
{
    [Function("Try",new string[] { "invoke" },invoking:true)]
    internal class FunctionTry : FunctionDefinition
    {
        public override string CallBase()
        {
            var tryBlock = ProvidedArgs.First("invoke");
            if (tryBlock == null)
                Compiler.ExceptionListener.Throw("Function Try must have an invoked function");
            var tryfunc = FunctionStack.First(tryBlock.ToString());
            if (tryfunc == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException,
                    $"Cannot find the invoked function.", LineValue));

            var catchExt = Extensions.FirstOrDefault(f => f.Name == "Catch") as ExtensionCatch;
            if (catchExt == null)
                Compiler.ExceptionListener.Throw("Function Try must have `Catch` extension");
            var catchBlock = catchExt.Extend();
            if (catchBlock.ElementAtOrDefault(0) == null)
                Compiler.ExceptionListener.Throw("Invoke for Catch block cannot be null");
            var catchfunc = FunctionStack.First(catchBlock[0].ToString());
            if (catchfunc == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException,
                    $"Cannot find the invoked function.", LineValue));

            Compiler.ExceptionListener.TryCatchEventStack.Add(new Exceptions.TryCatchEvent(tryfunc, catchfunc));

            tryfunc.TryParse(new TFunction(Caller.Function, new List<EDefinition>(), tryfunc.GetInvokeProperties(), Caller.CallingFunction));


            return "";
        }
    }
}
