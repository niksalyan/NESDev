
using NTOSDev.Components;
using ScintillaNet.Abstractions.Classes;
using ScintillaNet.Abstractions.Enumerations;
using ScintillaNet.WinForms;
using System.Diagnostics;
using WeifenLuo.WinFormsUI.Docking;

namespace NTOSDev.Controls
{
    public partial class CodeEditor : DockContent
    {

        private Scintilla scintilla;
        private bool fileSaved = false;
        public string filePath;

        public static string currentFile = string.Empty;
        public static Dictionary<string, CodeEditor> codeViews = new Dictionary<string, CodeEditor>();

        public static CodeEditor? CurrentView => !string.IsNullOrWhiteSpace(currentFile) && codeViews.ContainsKey(currentFile) ? codeViews[currentFile] : null;

        private bool allowSaving = true;

        public CodeEditor(string filePath)
        {
            InitializeComponent();
            InitializeScintilla();
            InitializeErrorDisplay();
            scintilla.TextChanged += ScintillaEditor_TextChanged;
            if (File.Exists(filePath))
            {
                if (Path.GetExtension(filePath).ToLower() == ".ntx")
                {
                    allowSaving = false;
                    this.filePath = filePath;
                    // scintilla.Text = BytecodeProgram.ToArduinoArray(File.ReadAllBytes(filePath), 16);
                    scintilla.ReadOnly = true;
                    fileSaved = true;
                    UpdateFileName();
                }
                else
                {
                    this.filePath = filePath;
                    scintilla.Text = File.ReadAllText(filePath);
                    fileSaved = true;
                    UpdateFileName();
                }

            }

            scintilla.GotFocus += ScintillaEditor_GotFocus;

        }

        private void ScintillaEditor_GotFocus(object? sender, EventArgs e)
        {
            ReloadEmulator();
        }

        private void ReloadEmulator(bool force = false)
        {
            if (!allowSaving) return;
            if (currentFile == filePath && !force) return;
            currentFile = filePath;
            codeViews[filePath] = this;
            DEmulator.RefreshEmulator();
        }

        private void ScintillaEditor_TextChanged(object? sender, EventArgs e)
        {
            fileSaved = false;
            UpdateFileName();
        }

        private void UpdateFileName()
        {
            Text = Path.GetFileName(filePath) + (fileSaved ? "" : "*");
        }

        private void InitializeScintilla()
        {
            scintilla = new Scintilla();
            scintilla.BorderStyle = BorderStyle.None;
            scintilla.Dock = DockStyle.Fill;

            scintilla.WrapMode = WrapMode.None;
            scintilla.IndentWidth = 4;
            scintilla.TabWidth = 4;
            scintilla.UseTabs = true;

            scintilla.Margins[0].Type = MarginType.Number;
            scintilla.Margins[0].Width = 40;

            scintilla.Styles[StyleConstants.Default].Font = "Consolas";
            scintilla.Styles[StyleConstants.Default].Size = 11;
            scintilla.Styles[StyleConstants.Default].ForeColor =
                Color.FromArgb(220, 220, 220);
            scintilla.Styles[StyleConstants.Default].BackColor =
                Color.FromArgb(30, 30, 30);

            // Your Scintilla build has the C++ lexer
            scintilla.LexerName = "cpp";

            scintilla.StyleClearAll();

            // Default
            scintilla.Styles[0].ForeColor =
                Color.FromArgb(220, 220, 220);
            scintilla.Styles[0].BackColor =
                Color.FromArgb(30, 30, 30);

            // Comments
            scintilla.Styles[1].ForeColor =
                Color.FromArgb(106, 153, 85);

            scintilla.Styles[2].ForeColor =
                Color.FromArgb(106, 153, 85);

            // Numbers
            scintilla.Styles[4].ForeColor =
                Color.FromArgb(181, 206, 168);

            // Keywords
            scintilla.Styles[5].ForeColor =
                Color.FromArgb(86, 156, 214);

            // Strings
            scintilla.Styles[6].ForeColor =
                Color.FromArgb(206, 145, 120);

            // Character
            scintilla.Styles[7].ForeColor =
                Color.FromArgb(206, 145, 120);

            // Operators
            scintilla.Styles[10].ForeColor =
                Color.FromArgb(220, 220, 170);



            // C++ lexer keywords, but populated with our NTOS/JS vocabulary
            scintilla.SetKeywords(
                0,
                @$"
using break case const continue debug default delete do else export extends false finally for
from function get if import in instanceof let new null of return set static super switch this
throw true try typeof var void while with yield loop init
drawBox fillBox drawRoundBox fillRoundBox cursor print printCentered printRight delay sync confirm alert confirmNumber editText dialog loadStr saveStr
cls fillCircle drawCircle drawPixel drawLine load drawSprite collision
abs min max clamp sign sqrt pow hypot sin cos tan asin acos atan2 floor ceil round fmod lerp map rnd
"
            );


            // ---------------------------------------------------------
            // Cursor
            // ---------------------------------------------------------

            scintilla.CaretForeColor =
                Color.FromArgb(255, 255, 255);

            scintilla.CaretWidth = 2;

            // Do not highlight the entire current line.
            scintilla.CaretLineVisible = false;


            // ---------------------------------------------------------
            // Line number bar
            // ---------------------------------------------------------

            scintilla.Styles[StyleConstants.LineNumber].ForeColor =
                Color.FromArgb(64, 64, 64);

            scintilla.Styles[StyleConstants.LineNumber].BackColor =
                Color.FromArgb(30, 30, 30);

            scintilla.Styles[StyleConstants.LineNumber].Size = 9;



            this.Controls.Add(scintilla);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveFile();
                return true; // handled
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(filePath) || !allowSaving)
                return;

            try
            {
                File.WriteAllText(filePath, scintilla.Text);
                fileSaved = true;
                UpdateFileName();
                ReloadEmulator(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save file: " + ex.Message);
            }
        }

        private const int ErrorIndicator = 8;
        private const int ErrorAnnotationStyle = 30;

        private void InitializeErrorDisplay()
        {
            // Error underline
            scintilla.Indicators[ErrorIndicator].Style =
                IndicatorStyle.Squiggle;

            scintilla.Indicators[ErrorIndicator].ForeColor =
                Color.Red;

            // Error annotation style
            scintilla.Styles[ErrorAnnotationStyle].ForeColor =
                Color.Red;

            scintilla.Styles[ErrorAnnotationStyle].BackColor =
                Color.FromArgb(255, 240, 240);

            scintilla.Styles[ErrorAnnotationStyle].Bold = true;

            scintilla.AnnotationVisible = Annotation.Standard;
        }

        public void ShowError(int line, int character, string error)
        {
            int lineIndex = line - 1;

            if (lineIndex < 0 || lineIndex >= scintilla.Lines.Count)
                return;

            int lineStart = scintilla.Lines[lineIndex].Position;
            int position = lineStart + character - 1;

            // Red squiggly underline
            scintilla.IndicatorCurrent = ErrorIndicator;
            scintilla.IndicatorFillRange(position, 1);

            // Error message
            scintilla.Lines[lineIndex].AnnotationText = error;

            // Apply our error style to the annotation.
            scintilla.Lines[lineIndex].AnnotationStyle =
                ErrorAnnotationStyle;

            // Scroll to the error without changing the selection.
            scintilla.ScrollRange(position, position);
        }



        private void ClearErrors()
        {
            try
            {
                scintilla.IndicatorCurrent = ErrorIndicator;
                scintilla.IndicatorClearRange(0, scintilla.TextLength);

                scintilla.AnnotationClearAll();
            }
            catch { } // clear silently
        }

        public static void ClearAllErrors()
        {
            foreach (var c in codeViews)
            {
                c.Value?.ClearErrors();
            }
        }

        public void InsertTextAtCaret(string text)
        {
            ClearErrors();
            scintilla.ReplaceSelection(text);
        }

        private void CodeEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            var item = codeViews.FirstOrDefault(x => x.Value == this);

            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                codeViews.Remove(item.Key);
            }
        }
    }
}
