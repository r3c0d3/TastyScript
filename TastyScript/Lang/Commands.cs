﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TastyScript.Android;
using TastyScript.Lang.Extensions;
using TastyScript.Lang.Tokens;

namespace TastyScript.Lang
{
    //The idea behind this static class is to rope all the lower level commands that functions
    //use into one static container to reduce the amount of work needed for future changes
    //and having all the commands in one place will make ios support much easier to implement
    internal static class Commands
    {
        public static void Connect(string i)
        {
            Main.AndroidDriver = new Android.Driver(i.UnCleanString());
        }
        public static void Tap(int x, int y)
        {
            Main.AndroidDriver.SendCommand($"input tap {x} {y}");
        }
        public static void LongTap(int x, int y, int duration)
        {
            Main.AndroidDriver.SendCommand($"input swipe {x} {y} {x} {y} {duration}");
        }
        public static void Swipe(int x1, int y1, int x2, int y2, int duration)
        {
            Main.AndroidDriver.SendCommand($"input swipe {x1} {y1} {x2} {y2} {duration}");
        }
        public static void KeyEvent(AndroidKeyCode code)
        {
            Main.AndroidDriver.SendCommand($"input keyevent {(int)code}");
        }
        public static void SendText(string text)
        {
            Main.AndroidDriver.SendCommand($"input text {text.UnCleanString().Replace(" ", "%s")}");
        }
        public static Image GetScreenshot()
        {
            return Main.AndroidDriver.GetScreenshot().Result;
        }
        public static void SetAppPackage(string pkg)
        {
            Main.AndroidDriver.SetAppPackage(pkg.UnCleanString());
        }
        public static bool CheckFocus()
        {
            return Main.AndroidDriver.CheckFocusedApp();
        }
        public static double[] GetImageCoordinates(string path, string[] prop)
        {
            AnalyzeScreen a = new AnalyzeScreen();
            var ret = a.GetScreenCoords(path.UnCleanString(), prop);
            return ret;
        }
        public static void AnalyzeScreen(string success, IBaseFunction successAction, IBaseFunction failureAction, string[] prop, TFunction caller = null)
        {
            var tfunc = new TFunction(caller.Function, new List<EDefinition>(), string.Join(",", caller.Function.GetInvokeProperties()), caller.CallingFunction);
            try
            {
                bool finished = false;
                bool func = false;
                Thread th = new Thread(() =>
                {
                    AnalyzeScreen ascreen = new AnalyzeScreen();
                    ascreen.Analyze(success.UnCleanString(),
                        () => { finished = true; func = true; },
                        () => { finished = true; },
                        prop
                    );
                });
                th.Start();
                Stopwatch watch = new Stopwatch();
                watch.Start();
                while (finished == false)
                {
                    if (TokenParser.Stop)
                        break;
                    Thread.Sleep(1000);//sleep for 1 second before checking again
                                       //if 30 seconds go by, then break and kill the thread
                    if (watch.Elapsed.TotalMilliseconds >= 45000)
                    {
                        Compiler.ExceptionListener.ThrowSilent(new ExceptionHandler(ExceptionType.SystemException,
                            $"CheckScreen() timed out."));
                        //smash the thread and move on. we dont care about that data anyway
                        try { th.Abort(); } catch (Exception e) { if (!(e is ThreadAbortException)) throw; }
                        break;
                    }
                }
                watch.Stop();
                if (func)
                    successAction.TryParse(tfunc);
                else
                    failureAction.TryParse(tfunc);
            }
            catch
            {
                Compiler.ExceptionListener.ThrowSilent(new ExceptionHandler("[63]Image check failed to execute. Continuing with failure function"));
                failureAction.TryParse(tfunc);
            }
        }
        public static void AnalyzeScreen(string success, string failure, IBaseFunction successAction, IBaseFunction failureAction, string[] prop, TFunction caller = null)
        {
            var tfunc = new TFunction(caller.Function, new List<EDefinition>(), string.Join(",", caller.Function.GetInvokeProperties()), caller.CallingFunction);
            try
            {
                bool finished = false;
                bool func = false;
                Thread th = new Thread(() =>
                {
                    AnalyzeScreen ascreen = new AnalyzeScreen();
                    ascreen.Analyze(success.UnCleanString(), failure.UnCleanString(),
                            () => { finished = true; func = true; },
                            () => { finished = true; },
                            prop
                        );
                });
                th.Start();
                Stopwatch watch = new Stopwatch();
                watch.Start();
                while (finished == false)
                {
                    if (TokenParser.Stop)
                        break;
                    Thread.Sleep(1000);//sleep for 1 second before checking again
                                       //if 30 seconds go by, then break and kill the thread
                    if (watch.Elapsed.TotalMilliseconds >= 45000)
                    {
                        Compiler.ExceptionListener.ThrowSilent(new ExceptionHandler(ExceptionType.SystemException,
                            $"CheckScreen() timed out."));
                        //smash the thread and move on. we dont care about that data anyway
                        try { th.Abort(); } catch (Exception e) { if (!(e is ThreadAbortException)) throw; }
                        break;
                    }
                }
                watch.Stop();
                if (func)
                    successAction.TryParse(tfunc);
                else
                    failureAction.TryParse(tfunc);
            }
            catch
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler("[63]Image check failed to execute. Continuing with failure function"));
            }

        }
    }
}
