﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PrologOnBrowser.Services.ConsoleHost
{
    public class ConsoleHostService : IConsoleHost
    {
        private readonly List<ConsoleLine> _Lines = new();

        public IEnumerable<ConsoleLine> Lines => _Lines;

        private ConsoleLine? _CurrentLine = null;

        private string _CurrentForeColor = "#cccccc";

        private int _IdSequence = 0;

        public event EventHandler? StateHasChanged;

        public IConsoleHost Write(string? text)
        {
            text ??= "";
            if (_CurrentLine == null)
            {
                _CurrentLine = NewLine();
            }
            var lines = text.Split('\n').Select(t => t.TrimEnd('\r')).ToArray();
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0) _CurrentLine = NewLine();
                _CurrentLine.AddFragments(CreateFragments(lines[l]));
            }

            StateHasChanged?.Invoke(this, EventArgs.Empty);
            return this;
        }

        public IConsoleHost WriteLine() => WriteLine("");

        public IConsoleHost WriteLine(string? text)
        {
            var targetLine = _CurrentLine ?? NewLine();
            text ??= "";

            var lines = text.Split('\n').Select(t => t.TrimEnd('\r')).ToArray();
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0) targetLine = NewLine();
                targetLine.AddFragments(CreateFragments(lines[l]));
            }

            _CurrentLine = null;
            StateHasChanged?.Invoke(this, EventArgs.Empty);
            return this;
        }

        private ConsoleLine NewLine()
        {
            var line = new ConsoleLine(_IdSequence++);
            _Lines.Add(line);
            return line;
        }

        public void Clear()
        {
            _Lines.Clear();
            StateHasChanged?.Invoke(this, EventArgs.Empty);
        }

        private IEnumerable<ConsoleFragment> CreateFragments(string text)
        {
            var ansiColorPatterns = Regex.Matches(text, "\x1b\\[\\d+m")
                .Select(m => (m.Success, m.Value, m.Index, m.Length))
                .ToList();

            if (ansiColorPatterns.Count == 0 || ansiColorPatterns[0].Index > 0)
                ansiColorPatterns.Insert(0, (false, "", 0, 0));

            var lastPattern = ansiColorPatterns[ansiColorPatterns.Count - 1];
            if (ansiColorPatterns.Count == 1 || lastPattern.Index + lastPattern.Length < text.Length)
                ansiColorPatterns.Add((false, "", text.Length, 0));

            for (var i = 0; i < ansiColorPatterns.Count - 1; i++)
            {
                var headPattern = ansiColorPatterns[i];
                var tailPattern = ansiColorPatterns[i + 1];
                UpdateCurrentForeColor(headPattern);

                var fragmentIndex = headPattern.Index + headPattern.Length;
                var fragmentLength = tailPattern.Index - fragmentIndex;
                if (i == 0 || fragmentLength > 0)
                {
                    var textFragment = text.Substring(fragmentIndex, fragmentLength);

                    foreach (var fragment in CreateHyperLinkedFragments(textFragment))
                    {
                        yield return fragment;
                    }
                }
                UpdateCurrentForeColor(tailPattern);
            }
        }

        private IEnumerable<ConsoleFragment> CreateHyperLinkedFragments(string text)
        {
            var linkPatterns = Regex.Matches(text, @"\[(?<text>[^\]]+?)\]\((?<link>[^)]+?)\)")
                .Select(m => (Text: m.Groups["text"].Value, Link: m.Groups["link"].Value, m.Index, m.Length))
                .ToList();

            if (!linkPatterns.Any())
            {
                yield return new ConsoleFragment(_IdSequence++, text, _CurrentForeColor, link: null);
                yield break;
            }

            var linkPatPos = 0;
            var textPos = 0;
            while (textPos < text.Length)
            {
                var pattern = linkPatPos < linkPatterns.Count ? linkPatterns[linkPatPos++] : ("", null, text.Length, 0);
                var textLen = pattern.Index - textPos;
                if (textLen > 0) yield return new ConsoleFragment(_IdSequence++, text.Substring(textPos, textLen), _CurrentForeColor, null);

                textPos += textLen;

                if (pattern.Length > 0)
                    yield return new ConsoleFragment(_IdSequence++, pattern.Text, _CurrentForeColor, pattern.Link);

                textPos += pattern.Length;
            }
        }

        private void UpdateCurrentForeColor((bool Success, string Value, int Index, int Length) ansiColorPattern)
        {
            if (ansiColorPattern.Success)
                _CurrentForeColor = ANSIColorToRGB.TryGetRGB(ansiColorPattern.Value, out var rgb) ? rgb : _CurrentForeColor;
        }
    }
}
