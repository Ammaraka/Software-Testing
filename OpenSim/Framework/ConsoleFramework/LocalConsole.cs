/*
 * Copyright (c) Contributors, http://aurora-sim.org/, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using Nini.Config;
using log4net.Core;

namespace OpenSim.Framework
{
    /// <summary>
    /// A console that uses cursor control and color
    /// </summary>
    public class LocalConsole : CommandConsole
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private readonly object m_syncRoot = new object();

        private int y = -1;
        private int cp = 0;
        private int h = 1;
        private StringBuilder cmdline = new StringBuilder();
        private bool echo = true;
        private List<string> history = new List<string>();

        private static readonly ConsoleColor[] Colors = {
            // the dark colors don't seem to be visible on some black background terminals like putty :(
            //ConsoleColor.DarkBlue,
            //ConsoleColor.DarkGreen,
            //ConsoleColor.Gray, 
            //ConsoleColor.DarkGray,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkYellow,
            ConsoleColor.Green,
            ConsoleColor.Blue,
            ConsoleColor.Magenta,
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.Cyan
        };

        public override void Initialize(string defaultPrompt, IConfigSource source, ISimulationBase baseOpenSim)
        {
            if (source.Configs["Console"] != null)
            {
                if (source.Configs["Console"].GetString("Console", Name) != Name)
                    return;
            }
            else
                return;

            baseOpenSim.ApplicationRegistry.RegisterModuleInterface<ICommandConsole>(this);

            m_Commands.AddCommand ("help", "help",
                    "Get a general command list", base.Help);
        }

        private static ConsoleColor DeriveColor(string input)
        {
            // it is important to do Abs, hash values can be negative
            return Colors[(Math.Abs(input.ToUpper().Length) % Colors.Length)];
        }

        public override string Name
        {
            get { return "LocalConsole"; }
        }

        private void AddToHistory(string text)
        {
            while (history.Count >= 100)
                history.RemoveAt(0);

            history.Add(text);
        }

        /// <summary>
        /// Set the cursor row.
        /// </summary>
        ///
        /// <param name="top">
        /// Row to set.  If this is below 0, then the row is set to 0.  If it is equal to the buffer height or greater
        /// then it is set to one less than the height.
        /// </param>
        /// <returns>
        /// The new cursor row.
        /// </returns>
        private int SetCursorTop(int top)
        {
            // From at least mono 2.4.2.3, window resizing can give mono an invalid row and column values.  If we try
            // to set a cursor row position with a currently invalid column, mono will throw an exception.
            // Therefore, we need to make sure that the column position is valid first.
            int left = System.Console.CursorLeft;

            if (left < 0)
            {
                System.Console.CursorLeft = 0;
            }
            else 
            {
                int bw = System.Console.BufferWidth;
                
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bw > 0 && left >= bw)
                    System.Console.CursorLeft = bw - 1;
            }
            
            if (top < 0)
            {
                top = 0;
            }
            else
            {
                int bh = System.Console.BufferHeight;
                
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bh > 0 && top >= bh)
                    top = bh - 1;
            }

            System.Console.CursorTop = top;

            return top;
        }

        /// <summary>
        /// Set the cursor column.
        /// </summary>
        ///
        /// <param name="left">
        /// Column to set.  If this is below 0, then the column is set to 0.  If it is equal to the buffer width or greater
        /// then it is set to one less than the width.
        /// </param>
        /// <returns>
        /// The new cursor column.
        /// </returns>
        private int SetCursorLeft(int left)
        {
            // From at least mono 2.4.2.3, window resizing can give mono an invalid row and column values.  If we try
            // to set a cursor column position with a currently invalid row, mono will throw an exception.
            // Therefore, we need to make sure that the row position is valid first.
            int top = System.Console.CursorTop;

            if (top < 0)
            {
                System.Console.CursorTop = 0;
            }
            else 
            {
                int bh = System.Console.BufferHeight;
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bh > 0 && top >= bh)
                    System.Console.CursorTop = bh - 1;
            }
            
            if (left < 0)
            {
                left = 0;
            }
            else
            {
                int bw = System.Console.BufferWidth;

                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bw > 0 && left >= bw)
                    left = bw - 1;
            }

            System.Console.CursorLeft = left;

            return left;
        }

        protected string prompt = "# ";

        private void Show()
        {
            lock (cmdline)
            {
                if (y == -1 || System.Console.BufferWidth == 0)
                    return;

                int xc = prompt.Length + cp;
                int new_x = xc % System.Console.BufferWidth;
                int new_y = y + xc / System.Console.BufferWidth;
                int end_y = y + (cmdline.Length + prompt.Length) / System.Console.BufferWidth;
                if (end_y / System.Console.BufferWidth >= h)
                    h++;
                if (end_y >= System.Console.BufferHeight) // wrap
                {
                    y--;
                    new_y--;
                    SetCursorLeft(0);
                    SetCursorTop(System.Console.BufferHeight - 1);
                    System.Console.WriteLine(" ");
                }

                y = SetCursorTop(y);
                SetCursorLeft(0);

                if (echo)
                    System.Console.Write("{0}{1}", prompt, cmdline);
                else
                    System.Console.Write("{0}", prompt);

                SetCursorTop(new_y);
                SetCursorLeft(new_x);
            }
        }

        public override void LockOutput()
        {
            Monitor.Enter(cmdline);
            try
            {
                if (y != -1)
                {
                    y = SetCursorTop(y);
                    System.Console.CursorLeft = 0;

                    int count = cmdline.Length + prompt.Length;

                    while (count-- > 0)
                        System.Console.Write(" ");

                    y = SetCursorTop(y);
                    SetCursorLeft(0);
                }
            }
            catch (Exception)
            {
            }
        }

        public override void UnlockOutput()
        {
            if (y != -1)
            {
                y = System.Console.CursorTop;
                Show();
            }
            Monitor.Exit(cmdline);
        }

        private void WriteColorText(ConsoleColor color, string sender)
        {
            try
            {
                lock (this)
                {
                    try
                    {
                        System.Console.ForegroundColor = color;
                        System.Console.Write(sender);
                        System.Console.ResetColor();
                    }
                    catch (ArgumentNullException)
                    {
                        // Some older systems dont support coloured text.
                        System.Console.WriteLine(sender);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void WriteLocalText (string text, Level level)
        {
            MainConsole.TriggerLog (level, text);
            string logtext = "";
            if (text != "")
            {
                int CurrentLine = 0;
                string[] Lines = text.Split(new char[2]{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
                //This exists so that we don't have issues with multiline stuff, since something is messed up with the Regex
                foreach (string line in Lines)
                {
                    string[] split = line.Split (new string[2] { "[", "]" }, StringSplitOptions.None);
                    int currentPos = 0;
                    int boxNum = 0;
                    foreach (string s in split)
                    {
                        if (line[currentPos] == '[')
                        {
                            if (level == Level.Alert)
                                WriteColorText(ConsoleColor.Magenta, "[");
                            else if (level.Value >= Level.Fatal.Value)
                                WriteColorText(ConsoleColor.White, "[");
                            else if (level.Value >= Level.Error.Value)
                                WriteColorText(ConsoleColor.Red, "[");
                            else if (level.Value >= Level.Warn.Value)
                                WriteColorText (ConsoleColor.Yellow, "[");
                            else
                                WriteColorText (ConsoleColor.Gray, "[");
                            boxNum++;
                            currentPos++;
                        }
                        else if (line[currentPos] == ']')
                        {
                            if (level == Level.Error)
                                WriteColorText (ConsoleColor.Red, "]");
                            else if (level == Level.Warn)
                                WriteColorText (ConsoleColor.Yellow, "]");
                            else if (level == Level.Alert)
                                WriteColorText (ConsoleColor.Magenta, "]");
                            else
                                WriteColorText (ConsoleColor.Gray, "]");
                            boxNum--;
                            currentPos++;
                        }
                        if (boxNum == 0)
                        {
                            if (level == Level.Error)
                                WriteColorText (ConsoleColor.Red, s);
                            else if (level == Level.Warn)
                                WriteColorText (ConsoleColor.Yellow, s);
                            else if (level == Level.Alert)
                                WriteColorText (ConsoleColor.Magenta, s);
                            else
                                WriteColorText (ConsoleColor.Gray, s);
                        }
                        else //We're in a box
                            WriteColorText (DeriveColor (s), s);
                        currentPos += s.Length; //Include the extra 1 for the [ or ]
                    }
                    
                    CurrentLine++;
                    if (Lines.Length - CurrentLine != 0)
                        System.Console.WriteLine();

                    logtext += line;
                }
            }

            System.Console.WriteLine();
        }

        public override void Output(string text)
        {
            Output(text, Level.Debug);
        }

        public override void Output(string text, Level level)
        {
            lock (cmdline)
            {
                if (y == -1)
                {
                    WriteLocalText(text, level);

                    return;
                }

                y = SetCursorTop(y);
                SetCursorLeft(0);

                int count = cmdline.Length + prompt.Length;

                while (count-- > 0)
                    System.Console.Write(" ");

                y = SetCursorTop(y);
                SetCursorLeft(0);

                WriteLocalText(text, level);

                y = System.Console.CursorTop;

                Show();
            }
        }

        private bool ContextHelp()
        {
            string[] words = Parser.Parse(cmdline.ToString());

            bool trailingSpace = cmdline.ToString().EndsWith(" ");

            // Allow ? through while typing a URI
            //
            if (words.Length > 0 && words[words.Length-1].StartsWith("http") && !trailingSpace)
                return false;

            string[] opts = Commands.FindNextOption(words);

            if (opts.Length == 0)
                Output ("No options.");
            else
                if (opts[0].StartsWith("Command help:"))
                    Output(opts[0]);
                 else
                    Output(String.Format("Options: {0}", String.Join("\n", opts)));

            return true;
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            h = 1;
            cp = 0;
            prompt = p;
            echo = e;
            int historyLine = history.Count;
            bool allSelected = false;

            SetCursorLeft(0); // Needed for mono
            System.Console.Write(" "); // Needed for mono

            lock (cmdline)
            {
                y = System.Console.CursorTop;
                cmdline.Remove(0, cmdline.Length);
            }

            while (true)
            {
                Show();

                ConsoleKeyInfo key = System.Console.ReadKey(true);
                char c = key.KeyChar;
                bool changed = false;

                if (!Char.IsControl(c))
                {
                    if (cp >= 318)
                        continue;

                    if (c == '?' && isCommand)
                    {
                        if (ContextHelp())
                            continue;
                    }

                    cmdline.Insert(cp, c);
                    cp++;
                }
                else
                {
                    switch (key.Key)
                    {
                    case ConsoleKey.Backspace:
                        if (cp == 0)
                            break;
                        string toReplace = " ";
                        if (allSelected)
                        {
                            for (int i = 0; i < cmdline.Length; i++)
                            {
                                toReplace += " ";
                            }
                            cmdline.Remove (0, cmdline.Length);
                            cp = 0;
                            allSelected = false;
                        }
                        else
                        {
                            cmdline.Remove (cp - 1, 1);
                            cp--;
                        }

                        SetCursorLeft(0);
                        y = SetCursorTop(y);

                        if (echo) //This space makes the last line part disappear
                            System.Console.Write ("{0}{1}", prompt, cmdline + toReplace);
                        else
                            System.Console.Write("{0}", prompt);

                        break;
                    case ConsoleKey.A:
                        if ((key.Modifiers | ConsoleModifiers.Control) == ConsoleModifiers.Control)
                            allSelected = true;
                        break;
                    case ConsoleKey.Delete:
                        if (cp == cmdline.Length || cp < 0)
                            break;
                        string stringToReplace = " ";
                        if (allSelected)
                        {
                            for (int i = 0; i < cmdline.Length; i++)
                            {
                                stringToReplace += " ";
                            }
                            cmdline.Remove (0, cmdline.Length);
                            cp = 0;
                            allSelected = false; //All done
                        }
                        else
                        {
                            cmdline.Remove (cp, 1);
                            cp--;
                        }

                        SetCursorLeft (0);
                        y = SetCursorTop (y);

                        if (echo) //This space makes the last line part disappear
                            System.Console.Write ("{0}{1}", prompt, cmdline + stringToReplace);
                        else
                            System.Console.Write ("{0}", prompt);

                        break;
                    case ConsoleKey.End:
                        cp = cmdline.Length;
                        allSelected = false;
                        break;
                    case ConsoleKey.Home:
                        cp = 0;
                        allSelected = false;
                        break;
                    case ConsoleKey.UpArrow:
                        if (historyLine < 1)
                            break;
                        allSelected = false;
                        historyLine--;
                        LockOutput();
                        cmdline.Remove(0, cmdline.Length);
                        cmdline.Append(history[historyLine]);
                        cp = cmdline.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.DownArrow:
                        if (historyLine >= history.Count)
                            break;
                        allSelected = false;
                        historyLine++;
                        LockOutput();
                        if (historyLine == history.Count)
                        {
                            cmdline.Remove(0, cmdline.Length);
                        }
                        else
                        {
                            cmdline.Remove(0, cmdline.Length);
                            cmdline.Append(history[historyLine]);
                        }
                        cp = cmdline.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.LeftArrow:
                            if (cp > 0)
                            {
                                changed = true;
                                cp--;
                            }
                            if (m_isPrompting && m_promptOptions.Count > 0)
                            {
                                int last = m_lastSetPromptOption;
                                if (changed)
                                    cp++;
                                if(m_lastSetPromptOption > 0)
                                    m_lastSetPromptOption--;
                                cmdline = new StringBuilder (m_promptOptions[m_lastSetPromptOption]);
                                string pr = m_promptOptions[m_lastSetPromptOption];
                                if ((last - m_lastSetPromptOption) != 0)
                                {
                                    int charDiff = m_promptOptions[last].Length - m_promptOptions[m_lastSetPromptOption].Length;
                                    for (int i = 0; i < charDiff; i++)
                                        pr += " ";
                                }
                                LockOutput ();
                                System.Console.CursorLeft = 0;
                                System.Console.Write ("{0}{1}", prompt, pr);
                                UnlockOutput ();
                            }
                        allSelected = false;
                        break;
                    case ConsoleKey.RightArrow:
                        if (cp < cmdline.Length)
                        {
                            changed = true;
                            cp++;
                        }
                        if (m_isPrompting && m_promptOptions.Count > 0)
                        {
                            int last = m_lastSetPromptOption;
                            if (m_lastSetPromptOption < m_promptOptions.Count - 1)
                                m_lastSetPromptOption++;
                            if (changed)
                                cp--;
                            cmdline = new StringBuilder (m_promptOptions[m_lastSetPromptOption]);
                            string pr = m_promptOptions[m_lastSetPromptOption];
                            if ((last - m_lastSetPromptOption) != 0)
                            {
                                int charDiff = m_promptOptions[last].Length - m_promptOptions[m_lastSetPromptOption].Length;
                                for (int i = 0; i < charDiff; i++)
                                    pr += " ";
                            }
                            LockOutput ();
                            System.Console.CursorLeft = 0;
                            System.Console.Write ("{0}{1}", prompt, pr);
                            UnlockOutput ();
                        }
                        allSelected = false;
                        break;
                    case ConsoleKey.Tab:
                        ContextHelp ();
                        allSelected = false;
                        break;
                    case ConsoleKey.Enter:
                        allSelected = false;
                        SetCursorLeft(0);
                        y = SetCursorTop(y);

                        if (echo)
                            System.Console.WriteLine("{0}{1}", prompt, cmdline);
                        else
                            System.Console.WriteLine("{0}", prompt);

                        lock (cmdline)
                        {
                            y = -1;
                        }
                        string commandLine = cmdline.ToString();

                        if (isCommand)
                        {
                            if (cmdline.ToString() == "clear console")
                            {
                                history.Clear();
                                System.Console.Clear();
                                return String.Empty;
                            }
                            string[] cmd = Commands.Resolve(Parser.Parse(commandLine));

                            if (cmd.Length != 0)
                            {
                                int i;

                                for (i=0 ; i < cmd.Length ; i++)
                                {
                                    if (cmd[i].Contains(" "))
                                        cmd[i] = "\"" + cmd[i] + "\"";
                                }
                            }
                            AddToHistory(commandLine);
                            return string.Empty;
                        }

                        // If we're not echoing to screen (e.g. a password) then we probably don't want it in history
                        if (echo && commandLine != "")
                            AddToHistory(commandLine);

                        return cmdline.ToString();
                    default:
                        allSelected = false;
                        break;
                    }
                }
            }
        }
    }
}
