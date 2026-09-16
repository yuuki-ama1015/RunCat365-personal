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
    /// Still-set editor: name + thumbnail grid of all frames (2–16). Selection drives remove/reorder.
    /// </summary>
    internal class StillSetForm : Form
    {
        private const int NAME_MAX_LENGTH = 30;

        private readonly StillSetRepository repository;
        private readonly Action<string>? onSaved;
        private readonly Action<string>? onDeleted;
        private readonly string? initialName;
        private readonly List<Bitmap> pendingFrames = [];
        private int selectedFrameIndex = -1;

        private TextBox nameTextBox = null!;
        private FlowLayoutPanel framePanel = null!;
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
            ClientSize = new Size(460, 520);
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
                Width = 420,
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

            framePanel = new FlowLayoutPanel
            {
                Location = new Point(16, 102),
                Size = new Size(320, 300),
                AutoScroll = true,
                BackColor = Color.FromArgb(55, 55, 55),
                BorderStyle = BorderStyle.FixedSingle,
                WrapContents = true
            };

            addButton = CreateButton("追加…", new Point(348, 102), AddFrames);
            removeButton = CreateButton("削除", new Point(348, 140), RemoveSelected);
            moveUpButton = CreateButton("上へ", new Point(348, 178), () => MoveSelected(-1));
            moveDownButton = CreateButton("下へ", new Point(348, 216), () => MoveSelected(1));

            hintLabel = new Label
            {
                Text = $"透過 PNG を {StillSetRepository.MIN_FRAME_COUNT}〜{StillSetRepository.MAX_FRAME_COUNT} 枚。トレイ用に自動リサイズします。",
                AutoSize = false,
                Size = new Size(420, 36),
                Location = new Point(16, 416),
                ForeColor = Color.FromArgb(170, 170, 170)
            };

            saveButton = CreateButton("保存", new Point(248, 464), SaveSet);
            saveButton.Size = new Size(90, 28);
            deleteButton = CreateButton("削除…", new Point(348, 464), DeleteSet);
            deleteButton.Size = new Size(90, 28);
            deleteButton.Enabled = !string.IsNullOrEmpty(initialName);

            var closeButton = CreateButton("閉じる", new Point(16, 464), Close);
            closeButton.Size = new Size(90, 28);

            Controls.AddRange(
                nameLabel,
                nameTextBox,
                framesLabel,
                framePanel,
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
            if (pendingFrames.Count > 0) selectedFrameIndex = 0;
            RebuildFrameGrid();
        }

        private void RebuildFrameGrid()
        {
            framePanel.SuspendLayout();
            foreach (Control oldTile in framePanel.Controls)
            {
                oldTile.Dispose();
            }
            framePanel.Controls.Clear();

            for (int i = 0; i < pendingFrames.Count; i++)
            {
                framePanel.Controls.Add(CreateFrameTile(i));
            }

            framePanel.ResumeLayout();
            UpdateActionState();
        }

        private Panel CreateFrameTile(int frameIndex)
        {
            var selected = frameIndex == selectedFrameIndex;
            var container = new Panel
            {
                Size = new Size(70, 76),
                Margin = new Padding(4),
                BackColor = selected ? Color.FromArgb(75, 95, 125) : Color.Transparent,
                Cursor = Cursors.Hand
            };

            var pictureBox = new PictureBox
            {
                Size = new Size(48, 48),
                Location = new Point(11, 0),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = pendingFrames[frameIndex],
                BackColor = Color.FromArgb(40, 40, 40),
                Cursor = Cursors.Hand
            };

            var indexLabel = new Label
            {
                Text = $"{frameIndex + 1}",
                Size = new Size(70, 18),
                Location = new Point(0, 52),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Color.FromArgb(170, 170, 170),
                Cursor = Cursors.Hand
            };

            void select(object? sender, EventArgs e) => SelectFrame(frameIndex);
            container.Click += select;
            pictureBox.Click += select;
            indexLabel.Click += select;

            container.Controls.Add(pictureBox);
            container.Controls.Add(indexLabel);
            return container;
        }

        private void SelectFrame(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= pendingFrames.Count) return;
            if (selectedFrameIndex == frameIndex) return;
            selectedFrameIndex = frameIndex;
            RebuildFrameGrid();
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
            if (pendingFrames.Count > 0 && selectedFrameIndex < 0)
            {
                selectedFrameIndex = 0;
            }
            RebuildFrameGrid();
        }

        private void RemoveSelected()
        {
            var index = selectedFrameIndex;
            if (index < 0 || index >= pendingFrames.Count) return;
            pendingFrames[index].Dispose();
            pendingFrames.RemoveAt(index);
            if (pendingFrames.Count == 0)
            {
                selectedFrameIndex = -1;
            }
            else
            {
                selectedFrameIndex = Math.Clamp(index, 0, pendingFrames.Count - 1);
            }
            RebuildFrameGrid();
        }

        private void MoveSelected(int delta)
        {
            var index = selectedFrameIndex;
            var target = index + delta;
            if (index < 0 || target < 0 || target >= pendingFrames.Count) return;
            (pendingFrames[index], pendingFrames[target]) = (pendingFrames[target], pendingFrames[index]);
            selectedFrameIndex = target;
            RebuildFrameGrid();
        }

        private void UpdateActionState()
        {
            var selected = selectedFrameIndex;
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
