using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace War3FontFix
{
    internal sealed class MainForm : Form
    {
        private readonly PatchService service;
        private readonly TextBox directory = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        private readonly Button browse = new Button { AutoSize = true };
        private static readonly bool Chinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        private static string L(string zh, string en) { return Chinese ? zh : en; }

        public MainForm(PatchService service, string initialDirectory)
        {
            this.service = service;
            Text = "War3 CJK Font Fix";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 440);
            MinimumSize = new Size(600, 360);
            StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = L("游戏目录（包含 Game.dll）", "Game directory (contains Game.dll)"), AutoSize = true }, 0, 0);
            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            browse.Text = L("浏览…", "Browse…");
            pathRow.Controls.Add(directory, 0, 0);
            pathRow.Controls.Add(browse, 1, 0);
            layout.Controls.Add(pathRow, 0, 1);
            AddAction(L("检查文件", "Check file"), delegate { Inspect(); });
            AddAction(L("安装 / 升级", "Install / upgrade"), delegate { Change(true); });
            AddAction(L("恢复原版", "Restore original"), delegate { Change(false); });
            layout.Controls.Add(actions, 0, 2);
            layout.Controls.Add(log, 0, 3);
            Controls.Add(layout);

            browse.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog { Description = L("选择包含 Game.dll 的游戏目录", "Select the directory containing Game.dll"), ShowNewFolderButton = false })
                {
                    if (Directory.Exists(directory.Text)) dialog.SelectedPath = directory.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        directory.Text = dialog.SelectedPath;
                        Run(Inspect);
                    }
                }
            };
            string besideTool = AppDomain.CurrentDomain.BaseDirectory;
            directory.Text = initialDirectory ?? (File.Exists(Path.Combine(besideTool, "Game.dll")) ? besideTool : "");
            Shown += delegate
            {
                if (directory.Text.Length > 0) Run(Inspect);
                else log.Text = L("请选择游戏目录，然后检查文件。安装或恢复前请退出游戏。", "Select your game directory and check the file. Exit the game before installing or restoring.");
            };
        }

        private void AddAction(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true, Margin = new Padding(0, 8, 12, 8) };
            button.Click += delegate { Run(action); };
            actions.Controls.Add(button);
        }

        private void Run(Action action)
        {
            actions.Enabled = browse.Enabled = directory.Enabled = false;
            UseWaitCursor = true;
            try { action(); }
            catch (Exception error)
            {
                string detail = error is UnsupportedFileException
                    ? L("此 Game.dll 的完整指纹不受支持，未修改文件。", error.Message) : error.Message;
                log.Text = L("操作停止：", "Operation stopped: ") + detail;
            }
            finally { actions.Enabled = browse.Enabled = directory.Enabled = true; UseWaitCursor = false; }
        }

        private void Inspect()
        {
            var result = service.Inspect(directory.Text);
            string status = !result.Supported ? L("不支持此文件，程序会拒绝修改。", "Unsupported file; modification is refused.")
                : result.Revision == 0 ? L("原版文件，可以安装补丁。", "Original file; patch available.")
                : result.Revision == result.Profile.currentRevision ? L("当前版补丁已安装。", "Current patch installed.")
                : L("较早的补丁版本，可以升级。", "Earlier patch revision; upgrade available.");
            log.Text = status + Environment.NewLine + Program.Describe(result);
        }

        private void Change(bool install)
        {
            bool changed = service.Change(directory.Text, install);
            Inspect();
            log.Text = (changed ? L("完成，文件校验通过。原版备份会保留。", "Completed and verified. The original backup is retained.")
                : L("已处于目标状态，无需修改。", "Already in the requested state; no changes needed."))
                + Environment.NewLine + Environment.NewLine + log.Text;
        }
    }
}
