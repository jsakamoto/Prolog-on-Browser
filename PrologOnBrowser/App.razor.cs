using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Prolog;
using PrologOnBrowser.Services;
using static Toolbelt.AnsiEscCode.Colorize;

namespace PrologOnBrowser
{
    public partial class App
    {
        private const string DefaultPrompt = "?-";

        private string Prompt = DefaultPrompt;

        private ElementReference CommandLineInput;

        private string CommandLineInputText { get; set; } = "";

        private CommandHistory CommandHistory { get; } = new CommandHistory();

        private PrologEngine PrologEngine { get; } = new PrologEngine(persistentCommandHistory: false);

        private bool NoSaveHistory;

        private TaskCompletionSource<string>? InputTextTaskSource = null;

        private bool _Initialized = false;

        protected override async Task OnInitializedAsync()
        {
            this.ConsoleHost.WriteLine(Green("Prolog on Browser - ver." + this.GetType().Assembly.GetName().Version!.ToString(3)));
            this.ConsoleHost.WriteLine(DarkGray("- Powered by [CSharpProlog](https://github.com/jsakamoto/CSharpProlog) - ver." + typeof(PrologEngine).Assembly.GetName().Version!.ToString(3)));
            this.ConsoleHost.WriteLine(DarkGray("- Powered by [Blazor WebAssembly](https://blazor.net) - " + RuntimeInformation.FrameworkDescription));
            this.ConsoleHost.WriteLine(DarkGray("- GitHub repository - [https://github.com/jsakamoto/prolog-on-browser](https://github.com/jsakamoto/prolog-on-browser)"));
            this.ConsoleHost.WriteLine();

            var interpreterTask = PrologInterpreterLoop();

            await TypeAndExecuteCommand("assert(human(socrates)).");
            await TypeAndExecuteCommand("assert(mortal(X) :- human(X)).");
            await TypeAndExecuteCommand("mortal(socrates).");

            _Initialized = true;

            await interpreterTask;
        }

        private async Task PrologInterpreterLoop()
        {
            for (; ; )
            {
                this.Prompt = DefaultPrompt;

                var inputText = await InputTextAsync();

                ConsoleHost.WriteLine(this.Prompt + " " + inputText);

                if (string.IsNullOrEmpty(inputText)) continue;

                this.PrologEngine.Query = inputText;
                var solved = false;
                var valDisplayedOnce = false;
                foreach (var solution in this.PrologEngine.GetEnumerator())
                {
                    var variableDumps = solution.VarValuesIterator
                        .Where(val => val.DataType != "namedvar")
                        .Select(val => $"{val.Name} = {val.Value}")
                        .ToArray();


                    if (variableDumps.Any() == false || solution.Solved == false)
                    {
                        var outputText = solution.ToString()?.Trim('\r', '\n', ' ') ?? "";
                        if (outputText == "false") ConsoleHost.WriteLine(Cyan("no"));
                        else if (outputText == "true") ConsoleHost.WriteLine(Cyan("yes"));
                        else ConsoleHost.WriteLine(Red(outputText));

                        solved = true;
                        break;
                    }

                    if (valDisplayedOnce) ConsoleHost.WriteLine();

                    for (var i = 0; i < variableDumps.Length - 1; i++)
                    {
                        var hasNextVal = (i + 1) < variableDumps.Length;
                        ConsoleHost.WriteLine(variableDumps[i] + (hasNextVal ? "," : ""));
                    }
                    var lastValDump = variableDumps.LastOrDefault() ?? "";
                    this.Prompt = lastValDump + " ?";
                    valDisplayedOnce = true;

                    var reply = "";
                    do { reply = await InputTextAsync(noSaveHistory: true); } while (reply != "" && reply != ";");

                    ConsoleHost.WriteLine(lastValDump + (" " + reply).TrimEnd());

                    if (reply == "")
                    {
                        solved = true;
                        this.Prompt = DefaultPrompt;
                        ConsoleHost.WriteLine(Cyan("yes"));
                        break;
                    }
                }

                if (solved == false) ConsoleHost.WriteLine(Cyan("no"));
                this.Prompt = DefaultPrompt;
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await CommandLineInput.FocusAsync();
            await JS.InvokeVoidAsync("Helper.scrollIntoView", this.CommandLineInput);
        }

        private Task<string> InputTextAsync(bool noSaveHistory = false)
        {
            this.NoSaveHistory = noSaveHistory;
            this.InputTextTaskSource = new TaskCompletionSource<string>();
            return this.InputTextTaskSource.Task;
        }

        private void OnKeyDownCommandLineInput(KeyboardEventArgs e)
        {
            if (!_Initialized) return;

            switch (e.Key)
            {
                case "Enter":
                    ExecuteCommand();
                    break;
                case "ArrowUp":
                    RecallHistory(CommandHistory.TryGetPrevious(out var prevCommand), prevCommand);
                    break;
                case "ArrowDown":
                    RecallHistory(CommandHistory.TryGetNext(out var nextCommand), nextCommand);
                    break;
                //case "Tab":
                //    CommandLineInputText = CommandCompletion.Completion(CommandLineInputText);
                //    StateHasChanged();
                //    break;
                default: break;
            }
        }

        private void RecallHistory(bool found, string commandText)
        {
            if (!found) return;
            CommandLineInputText = commandText;
            this.StateHasChanged();
        }

        private void ExecuteCommand()
        {
            if (!this.NoSaveHistory) this.CommandHistory.Push(this.CommandLineInputText);
            this.InputTextTaskSource?.TrySetResult(this.CommandLineInputText);
            this.CommandLineInputText = "";
            this.StateHasChanged();
        }

        private async Task TypeAndExecuteCommand(string text)
        {
            await Task.Delay(800);
            var r = new Random((int)(DateTime.Now.Ticks % int.MaxValue));
            foreach (var c in text)
            {
                CommandLineInputText += c;
                StateHasChanged();
                await Task.Delay(r.Next(30, 150));
            }
            await Task.Delay(400);

            ExecuteCommand();
        }

        private string GetTwitterShareButtonUrl()
        {
            const string description = "\"Prolog on Browser\"\nThis is the Prolog interactive interpreter running on a Web browser!";
            const string url = "https://jsakamoto.github.io/Prolog-on-Browser/";
            return "https://twitter.com/intent/tweet" +
                $"?text={Uri.EscapeDataString(description)}" +
                $"&hashtags=Prolog" +
                $"&hashtags=Blazor" +
                $"&url={Uri.EscapeDataString(url)}";
        }
    }
}
