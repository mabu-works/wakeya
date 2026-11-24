using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Wakeya
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        // Web版（wakeya）と同じ定数・仕様
        private const int ShuffleDuration = 1200; // シャッフル演出の時間（ミリ秒）
        private const int ShuffleInterval = 50; // 名前が切り替わる間隔（ミリ秒）
        private static readonly string[] Sample =
        {
            "田中", "佐藤", "鈴木", "高橋", "伊藤", "渡辺", "山本", "中村", "小林", "加藤", "吉田", "山田"
        };

        private static readonly Color Bg = Color.FromArgb(10, 15, 28);
        private static readonly Color Surface = Color.FromArgb(17, 26, 44);
        private static readonly Color Surface2 = Color.FromArgb(24, 38, 66);
        private static readonly Color TextColor = Color.FromArgb(236, 241, 248);
        private static readonly Color Muted = Color.FromArgb(157, 177, 199);
        private static readonly Color Accent = Color.FromArgb(106, 141, 255);
        private static readonly Color Accent2 = Color.FromArgb(45, 212, 191);

        private readonly TextBox participantsBox;
        private readonly Label countLabel;
        private readonly RadioButton modeGroups;
        private readonly RadioButton modeSize;
        private readonly Label groupCountLabel;
        private readonly NumericUpDown groupCountNum;
        private readonly Label groupSizeLabel;
        private readonly NumericUpDown groupSizeNum;
        private readonly Button splitBtn;
        private readonly Button reshuffleBtn;
        private readonly Button copyBtn;
        private readonly Label shuffleDisplay;
        private readonly Label splitMsg;
        private readonly FlowLayoutPanel resultPanel;
        private readonly System.Windows.Forms.Timer shuffleTimer;
        private readonly Stopwatch shuffleWatch = new Stopwatch();
        private readonly Random random = new Random();

        private bool splitting;
        private List<List<string>> lastGroups = new List<List<string>>();
        private List<string> animPool = new List<string>();
        private List<List<string>> animGroups = new List<List<string>>();

        public MainForm()
        {
            Text = "わけや for Windows｜グループ分け";
            ClientSize = new Size(1020, 700);
            MinimumSize = new Size(1036, 739);
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Yu Gothic UI", 10F);

            var title = new Label
            {
                Text = "わけや｜グループ分けツール",
                Font = new Font("Yu Gothic UI", 15F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(16, 12),
                AutoSize = true
            };
            Controls.Add(title);

            var pLabel = new Label
            {
                Text = "参加者（1行に1人）",
                ForeColor = Muted,
                Location = new Point(16, 56),
                AutoSize = true
            };
            Controls.Add(pLabel);

            countLabel = new Label
            {
                Text = "0人",
                ForeColor = Muted,
                Location = new Point(240, 56),
                AutoSize = true
            };
            Controls.Add(countLabel);

            participantsBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 82),
                Size = new Size(292, 300),
                BackColor = Surface,
                ForeColor = TextColor,
                Font = new Font("Yu Gothic UI", 10F),
                BorderStyle = BorderStyle.FixedSingle
            };
            participantsBox.TextChanged += (s, e) => UpdateCount();
            Controls.Add(participantsBox);

            var sampleBtn = MakeSubButton("サンプル", 16, 392, 142);
            sampleBtn.Click += (s, e) => { participantsBox.Text = string.Join(Environment.NewLine, Sample); };
            Controls.Add(sampleBtn);

            var clearBtn = MakeSubButton("クリア", 166, 392, 142);
            clearBtn.Click += (s, e) => { participantsBox.Text = ""; participantsBox.Focus(); };
            Controls.Add(clearBtn);

            var modeLabel = new Label
            {
                Text = "分け方",
                ForeColor = Muted,
                Location = new Point(16, 446),
                AutoSize = true
            };
            Controls.Add(modeLabel);

            modeGroups = new RadioButton
            {
                Text = "グループ数で指定",
                Checked = true,
                ForeColor = TextColor,
                Location = new Point(16, 474),
                AutoSize = true
            };
            modeGroups.CheckedChanged += (s, e) => SyncModeControls();
            Controls.Add(modeGroups);

            modeSize = new RadioButton
            {
                Text = "1グループの人数で指定",
                ForeColor = TextColor,
                Location = new Point(160, 474),
                AutoSize = true
            };
            modeSize.CheckedChanged += (s, e) => SyncModeControls();
            Controls.Add(modeSize);

            groupCountLabel = new Label
            {
                Text = "グループ数",
                ForeColor = Muted,
                Location = new Point(16, 510),
                AutoSize = true
            };
            Controls.Add(groupCountLabel);
            groupCountNum = new NumericUpDown
            {
                Minimum = 1, Maximum = 999, Value = 2,
                Location = new Point(120, 506), Width = 80,
                BackColor = Surface, ForeColor = TextColor
            };
            Controls.Add(groupCountNum);

            groupSizeLabel = new Label
            {
                Text = "1グループの人数",
                ForeColor = Muted,
                Location = new Point(16, 510),
                AutoSize = true,
                Visible = false
            };
            Controls.Add(groupSizeLabel);
            groupSizeNum = new NumericUpDown
            {
                Minimum = 1, Maximum = 999, Value = 3,
                Location = new Point(140, 506), Width = 80,
                BackColor = Surface, ForeColor = TextColor,
                Visible = false
            };
            Controls.Add(groupSizeNum);

            var note = new Label
            {
                Text = "同名でも別々の人として扱います（名簿の同姓同名対応）。グループ数で分ける場合は、人数差が最大1になるよう均等に分けます。",
                ForeColor = Muted,
                Location = new Point(16, 548),
                Size = new Size(292, 90)
            };
            Controls.Add(note);

            splitBtn = new Button
            {
                Text = "グループ分け",
                Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold),
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(332, 52),
                Size = new Size(200, 44)
            };
            splitBtn.FlatAppearance.BorderSize = 0;
            splitBtn.Click += (s, e) => Split();
            Controls.Add(splitBtn);

            reshuffleBtn = MakeSubButton("もう一度", 544, 52, 130);
            reshuffleBtn.Size = new Size(130, 44);
            reshuffleBtn.Enabled = false;
            reshuffleBtn.Click += (s, e) => Split();
            Controls.Add(reshuffleBtn);

            copyBtn = MakeSubButton("結果をコピー", 686, 52, 150);
            copyBtn.Size = new Size(150, 44);
            copyBtn.Enabled = false;
            copyBtn.Click += (s, e) => CopyResult();
            Controls.Add(copyBtn);

            shuffleDisplay = new Label
            {
                Text = "",
                Font = new Font("Yu Gothic UI", 18F, FontStyle.Bold),
                ForeColor = Accent2,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(332, 108),
                Size = new Size(672, 52)
            };
            Controls.Add(shuffleDisplay);

            splitMsg = new Label
            {
                Text = "",
                ForeColor = Muted,
                Location = new Point(332, 166),
                Size = new Size(672, 26)
            };
            Controls.Add(splitMsg);

            resultPanel = new FlowLayoutPanel
            {
                Location = new Point(332, 200),
                Size = new Size(672, 468),
                AutoScroll = true,
                BackColor = Bg,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            Controls.Add(resultPanel);

            shuffleTimer = new System.Windows.Forms.Timer { Interval = ShuffleInterval };
            shuffleTimer.Tick += ShuffleTick;

            UpdateCount();
            SyncModeControls();
        }

        private Button MakeSubButton(string text, int x, int y, int w)
        {
            return new Button
            {
                Text = text,
                BackColor = Surface2,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(w, 36)
            };
        }

        private string CurrentMode() => modeSize.Checked ? "size" : "groups";

        private void SyncModeControls()
        {
            bool groups = CurrentMode() == "groups";
            groupCountLabel.Visible = groups;
            groupCountNum.Visible = groups;
            groupSizeLabel.Visible = !groups;
            groupSizeNum.Visible = !groups;
        }

        // 空行は無視する。同名でも別々の人として扱う（Web版 parseParticipants と同じ）
        private List<string> ParseParticipants()
        {
            var result = new List<string>();
            foreach (var line in participantsBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var name = line.Trim();
                if (name.Length == 0) continue;
                result.Add(name);
            }
            return result;
        }

        private void UpdateCount()
        {
            countLabel.Text = ParseParticipants().Count + "人";
        }

        private List<string> Shuffled(List<string> list)
        {
            var arr = list.ToArray();
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return new List<string>(arr);
        }

        // グループ数に対して、人数差が最大1になるよう均等に分ける（Web版 splitIntoGroups と同じ）
        private List<List<string>> SplitIntoGroups(List<string> participants, int groupCount)
        {
            var shuffled = Shuffled(participants);
            int baseSize = shuffled.Count / groupCount;
            int remainder = shuffled.Count % groupCount;
            var groups = new List<List<string>>();
            int idx = 0;
            for (int g = 0; g < groupCount; g++)
            {
                int size = baseSize + (g < remainder ? 1 : 0);
                groups.Add(shuffled.GetRange(idx, size));
                idx += size;
            }
            return groups;
        }

        // 設定を検証し、問題なければ groupCount を、問題があれば error を返す（Web版 resolveGroupCount と同じ）
        private (int groupCount, string error) ResolveGroupCount(List<string> participants)
        {
            if (participants.Count == 0) return (0, "参加者を1人以上入れてください。");
            if (participants.Count < 2) return (0, "グループ分けには参加者が2人以上必要です。");
            if (CurrentMode() == "groups")
            {
                int n = (int)groupCountNum.Value;
                if (n < 1) return (0, "グループ数は1以上で指定してください。");
                if (n > participants.Count)
                {
                    return (0, "参加者（" + participants.Count + "人）よりグループ数が多いです。空のグループができてしまいます。");
                }
                return (n, "");
            }
            int size = (int)groupSizeNum.Value;
            if (size < 1) return (0, "1グループの人数は1人以上で指定してください。");
            if (size > participants.Count)
            {
                return (0, "1グループの人数が参加者（" + participants.Count + "人）を超えています。");
            }
            return ((int)Math.Ceiling(participants.Count / (double)size), "");
        }

        private void Split()
        {
            if (splitting) return;
            var participants = ParseParticipants();
            var (groupCount, error) = ResolveGroupCount(participants);
            if (error.Length > 0)
            {
                splitMsg.Text = error;
                return;
            }
            var groups = SplitIntoGroups(participants, groupCount);

            // シャッフル演出（Web版 animateAndFinish と同じ 1200ms / 50ms）
            splitting = true;
            UpdateButtons();
            splitMsg.Text = "";
            resultPanel.Controls.Clear();
            animPool = participants;
            animGroups = groups;
            shuffleWatch.Restart();
            shuffleTimer.Start();
        }

        private void ShuffleTick(object? sender, EventArgs e)
        {
            if (shuffleWatch.ElapsedMilliseconds >= ShuffleDuration)
            {
                shuffleTimer.Stop();
                shuffleWatch.Stop();
                splitting = false;
                FinishSplit(animGroups);
                return;
            }
            shuffleDisplay.Text = animPool[random.Next(animPool.Count)];
        }

        private void FinishSplit(List<List<string>> groups)
        {
            lastGroups = groups.ConvertAll(g => new List<string>(g));
            shuffleDisplay.Text = "🎉 グループ分け完了（" + groups.Count + "グループ）";

            resultPanel.Controls.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                var members = groups[i];
                var card = new Panel
                {
                    BackColor = Surface,
                    Width = 216,
                    Height = 52 + members.Count * 22,
                    Margin = new Padding(6),
                    Padding = new Padding(10)
                };
                var header = new Label
                {
                    Text = "グループ" + (i + 1) + "（" + members.Count + "人）",
                    Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
                    ForeColor = Accent2,
                    Location = new Point(10, 8),
                    AutoSize = true
                };
                card.Controls.Add(header);
                var memberLabel = new Label
                {
                    Text = string.Join(Environment.NewLine, members),
                    ForeColor = TextColor,
                    Location = new Point(10, 34),
                    Size = new Size(196, Math.Max(20, members.Count * 22))
                };
                card.Controls.Add(memberLabel);
                resultPanel.Controls.Add(card);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            splitBtn.Enabled = !splitting;
            reshuffleBtn.Enabled = !splitting && lastGroups.Count > 0;
            copyBtn.Enabled = !splitting && lastGroups.Count > 0;
        }

        // 結果テキスト（Web版 resultText と同じ形式）
        private string ResultText()
        {
            var lines = new List<string> { "わけや グループ分け結果" };
            for (int i = 0; i < lastGroups.Count; i++)
            {
                lines.Add("グループ" + (i + 1) + "（" + lastGroups[i].Count + "人）：" + string.Join("、", lastGroups[i]));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private void CopyResult()
        {
            if (lastGroups.Count == 0) return;
            Clipboard.SetText(ResultText());
            splitMsg.Text = "結果をコピーしました。";
        }
    }
}
