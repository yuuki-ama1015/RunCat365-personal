// Copyright 2025 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using RunCat365.Properties;

namespace RunCat365
{
    /// <summary>
    /// Minimal still-set editor: name + ordered PNG list (2–16). YAGNI vs CustomRunnerForm.
    /// </summary>
    internal class StillSetForm : Form
    {
        private const int NAME_MAX_LENGTH = 30;

        private readonly StillSetRepository repository;
        private readonly Action<string>? onSaved;
        private readonly Action<string>? onDeleted;
        private readonly string? initialName;
        private readonly List<Bitmap> pendingFrames = [];

        private TextBox nameTextBox = null!;
        private ListBox frameListBox = null!;
        private Button addButton = null!;
        private Button removeButton = null!;
        private Button moveUpButton = null!;
        private Button moveDownButton = null!;
        private Button saveButton = null!;
        private Button deleteButton = null!;
        private Label hintLabel = null!;

        internal StillSetForm(
            StillSetRepository repository,
            string? editName = null,
            Action<string>? onSaved = null,
            Action<string>? onDeleted = null
        )
        {
            this.repository = repository;
            this.onSaved = onSaved;
            this.onDeleted = onDeleted;
            initialName = editName;

            Text = "静止画モード用 — 編集";
            Icon = Resources.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 460);
            BackColor = Color.FromArgb(45, 45, 45);
            ForeColor = Color.White;
            Padding = new Padding(16);

            BuildUi();
            LoadExisting(editName);
            UpdateActionState();
        }

        private void BuildUi()
        {
            var nameLabel = new Label
            {
                Text = "名前",
                AutoSize = true,
                Location = new Point(16, 16)
            };
            nameTextBox = new TextBox
            {
                Location = new Point(16, 40),
                Width = 380,
                MaxLength = NAME_MAX_LENGTH,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            nameTextBox.TextChanged += (_, _) => UpdateActionState();

            var framesLabel = new Label
            {
                Text = "フレーム（低負荷 → 高負荷）",
                AutoSize = true,
                Location = new Point(16, 78)
            };
            frameListBox = new ListBox
            {
                Location = new Point(16, 102),
                Size = new Size(280, 260),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            frameListBox.SelectedIndexChanged += (_, _) => UpdateActionState();

            addButton = CreateButton("追加…", new Point(308, 102), AddFrames);
            removeButton = CreateButton("削除", new Point(308, 140), RemoveSelected);
            moveUpButton = CreateButton("上へ", new Point(308, 178), () => MoveSelected(-1));
            moveDownButton = CreateButton("下へ", new Point(308, 216), () => MoveSelected(1));

            hintLabel = new Label
            {
                Text = $"透過 PNG を {StillSetRepository.MIN_FRAME_COUNT}〜{StillSetRepository.MAX_FRAME_COUNT} 枚。トレイ用に自動リサイズします。",
                AutoSize = false,
                Size = new Size(380, 36),
                Location = new Point(16, 372),
                ForeColor = Color.FromArgb(170, 170, 170)
            };

            saveButton = CreateButton("保存", new Point(208, 416), SaveSet);
            saveButton.Size = new Size(90, 28);
            deleteButton = CreateButton("削除…", new Point(308, 416), DeleteSet);
            deleteButton.Size = new Size(90, 28);
            deleteButton.Enabled = !string.IsNullOrEmpty(initialName);

            var closeButton = CreateButton("閉じる", new Point(16, 416), Close);
            closeButton.Size = new Size(90, 28);

            Controls.AddRange(
                nameLabel,
                nameTextBox,
                framesLabel,
                frameListBox,
                addButton,
                removeButton,
                moveUpButton,
                moveDownButton,
                hintLabel,
                closeButton,
                saveButton,
                deleteButton
            );
        }

        private Button CreateButton(string text, Point location, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(96, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            button.Click += (_, _) => onClick();
            return button;
        }

        private void LoadExisting(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var profile = repository.GetByName(name);
            if (profile is null) return;

            nameTextBox.Text = profile.Name;
            nameTextBox.ReadOnly = true;
            var frames = repository.LoadFrames(profile.Name);
            foreach (var frame in frames)
            {
                pendingFrames.Add(frame);
            }
            RefreshFrameList();
        }

        private void RefreshFrameList()
        {
            var selected = frameListBox.SelectedIndex;
            frameListBox.Items.Clear();
            for (int i = 0; i < pendingFrames.Count; i++)
            {
                frameListBox.Items.Add($"#{i + 1}  ({pendingFrames[i].Width}×{pendingFrames[i].Height})");
            }
            if (pendingFrames.Count > 0)
            {
                frameListBox.SelectedIndex = Math.Clamp(selected, 0, pendingFrames.Count - 1);
            }
            UpdateActionState();
        }

        private void AddFrames()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "PNG (*.png)|*.png",
                Multiselect = true,
                Title = "静止画モード用フレームを選択"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            foreach (var path in dialog.FileNames)
            {
                if (pendingFrames.Count >= StillSetRepository.MAX_FRAME_COUNT) break;
                try
                {
                    pendingFrames.Add(new Bitmap(path));
                }
                catch (Exception)
                {
                    // Skip unreadable files.
                }
            }
            RefreshFrameList();
        }

        private void RemoveSelected()
        {
            var index = frameListBox.SelectedIndex;
            if (index < 0 || index >= pendingFrames.Count) return;
            pendingFrames[index].Dispose();
            pendingFrames.RemoveAt(index);
            RefreshFrameList();
        }

        private void MoveSelected(int delta)
        {
            var index = frameListBox.SelectedIndex;
            var target = index + delta;
            if (index < 0 || target < 0 || target >= pendingFrames.Count) return;
            (pendingFrames[index], pendingFrames[target]) = (pendingFrames[target], pendingFrames[index]);
            frameListBox.SelectedIndex = target;
            RefreshFrameList();
            frameListBox.SelectedIndex = target;
        }

        private void UpdateActionState()
        {
            var selected = frameListBox.SelectedIndex;
            removeButton.Enabled = selected >= 0;
            moveUpButton.Enabled = selected > 0;
            moveDownButton.Enabled = selected >= 0 && selected < pendingFrames.Count - 1;
            var count = pendingFrames.Count;
            saveButton.Enabled = count >= StillSetRepository.MIN_FRAME_COUNT
                && count <= StillSetRepository.MAX_FRAME_COUNT
                && !string.IsNullOrWhiteSpace(nameTextBox.Text);
            hintLabel.Text = count is >= StillSetRepository.MIN_FRAME_COUNT and <= StillSetRepository.MAX_FRAME_COUNT
                ? $"透過 PNG を {StillSetRepository.MIN_FRAME_COUNT}〜{StillSetRepository.MAX_FRAME_COUNT} 枚。トレイ用に自動リサイズします。（{count} 枚）"
                : $"透過 PNG を {StillSetRepository.MIN_FRAME_COUNT}〜{StillSetRepository.MAX_FRAME_COUNT} 枚必要です。（現在 {count} 枚）";
        }

        private void SaveSet()
        {
            var name = nameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "名前を入力してください。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (pendingFrames.Count < StillSetRepository.MIN_FRAME_COUNT
                || pendingFrames.Count > StillSetRepository.MAX_FRAME_COUNT)
            {
                MessageBox.Show(
                    this,
                    $"フレームは {StillSetRepository.MIN_FRAME_COUNT}〜{StillSetRepository.MAX_FRAME_COUNT} 枚にしてください。",
                    "警告",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            if (string.IsNullOrEmpty(initialName)
                && repository.Exists(name)
                && MessageBox.Show(
                    this,
                    $"「{name}」は既にあります。上書きしますか？",
                    "確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                ) != DialogResult.Yes)
            {
                return;
            }

            if (!repository.Save(name, pendingFrames))
            {
                MessageBox.Show(this, "保存に失敗しました。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            onSaved?.Invoke(name);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteSet()
        {
            var name = initialName ?? nameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || !repository.Exists(name)) return;
            if (MessageBox.Show(
                    this,
                    $"「{name}」を削除しますか？",
                    "確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                ) != DialogResult.Yes)
            {
                return;
            }

            if (repository.Delete(name))
            {
                onDeleted?.Invoke(name);
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var frame in pendingFrames) frame.Dispose();
            pendingFrames.Clear();
            base.OnFormClosed(e);
        }
    }
}
