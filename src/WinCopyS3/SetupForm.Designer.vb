Imports System
Imports System.ComponentModel
Imports System.Windows.Forms

Namespace WinCopyS3
    Partial Public Class SetupForm
        ' Designer variables
        Private components As IContainer
        Private txtFolder As TextBox
        Private btnBrowse As Button
        Private txtBucket As TextBox
        Private txtRegion As TextBox
        Private txtAccessKey As TextBox
        Private txtSecret As TextBox
        Private btnEye As Button
        Private chkRebuildStartup As CheckBox
        Private chkRebuildSmoketest As CheckBox

        Private Sub InitializeComponent()
            Me.components = New Container()
            Me.Text = "Setup"
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.ClientSize = New Drawing.Size(520, 420)

            ' Group: Local
            Dim grpLocal As New GroupBox() With {.Left = 12, .Top = 12, .Width = 496, .Height = 84, .Text = "Local Configuration"}
            Dim lblFolder As New Label() With {.Left = 12, .Top = 22, .Text = "Local Folder", .AutoSize = True}
            txtFolder = New TextBox() With {.Left = 140, .Top = 20, .Width = 320}
            btnBrowse = New Button() With {.Left = 466, .Top = 18, .Width = 30, .Text = "..."}
            Dim lblFolderExample As New Label() With {.Left = 140, .Top = 48, .AutoSize = True, .ForeColor = Drawing.Color.DarkGray}
            grpLocal.Controls.AddRange(New Control() {lblFolder, txtFolder, btnBrowse, lblFolderExample})

            ' Group: S3
            Dim grpS3 As New GroupBox() With {.Left = 12, .Top = 108, .Width = 496, .Height = 160, .Text = "S3 Connection Details"}
            Dim lblBucket As New Label() With {.Left = 12, .Top = 22, .Text = "Bucket Name", .AutoSize = True}
            txtBucket = New TextBox() With {.Left = 140, .Top = 20, .Width = 320}
            Dim lblRegion As New Label() With {.Left = 12, .Top = 56, .Text = "Region", .AutoSize = True}
            txtRegion = New TextBox() With {.Left = 140, .Top = 54, .Width = 160}
            Dim lblAccess As New Label() With {.Left = 12, .Top = 92, .Text = "Access Key Id", .AutoSize = True}
            txtAccessKey = New TextBox() With {.Left = 140, .Top = 90, .Width = 220}
            Dim lblSecret As New Label() With {.Left = 12, .Top = 128, .Text = "Secret Access Key", .AutoSize = True}
            txtSecret = New TextBox() With {.Left = 140, .Top = 126, .Width = 220, .UseSystemPasswordChar = True}
            btnEye = New Button() With {.Left = 370, .Top = 124, .Width = 26, .Text = "441"}
            grpS3.Controls.AddRange(New Control() {lblBucket, txtBucket, lblRegion, txtRegion, lblAccess, txtAccessKey, lblSecret, txtSecret, btnEye})

            ' Group: Cache
            Dim grpCache As New GroupBox() With {.Left = 12, .Top = 280, .Width = 496, .Height = 96, .Text = "Cache Management Options"}
            chkRebuildStartup = New CheckBox() With {.Left = 16, .Top = 24, .Width = 460, .Text = "Rebuild cache from S3 on startup"}
            chkRebuildSmoketest = New CheckBox() With {.Left = 16, .Top = 48, .Width = 460, .Text = "Rebuild cache from S3 during smoketest"}
            grpCache.Controls.AddRange(New Control() {chkRebuildStartup, chkRebuildSmoketest})

            ' Buttons
            Dim btnOk As New Button() With {.Left = 360, .Top = 384, .Width = 64, .Text = "Save", .DialogResult = DialogResult.OK}
            Dim btnCancel As New Button() With {.Left = 444, .Top = 384, .Width = 64, .Text = "Cancel", .DialogResult = DialogResult.Cancel}

            ' Tooltips
            Dim tt As New ToolTip(Me.components)
            tt.AutoPopDelay = 10000
            tt.InitialDelay = 500
            tt.ReshowDelay = 200
            tt.ShowAlways = True
            tt.SetToolTip(txtFolder, "Local folder to monitor for new files. Use the browse button to pick a path.")
            tt.SetToolTip(btnBrowse, "Open folder browser")
            tt.SetToolTip(txtBucket, "Target S3 bucket name where files will be uploaded.")
            tt.SetToolTip(txtRegion, "AWS region (e.g., us-east-1). If using MinIO set ServiceURL in config.json instead.")
            tt.SetToolTip(txtAccessKey, "AWS Access Key ID for the IAM user with PutObject/ListBucket permissions.")
            tt.SetToolTip(txtSecret, "AWS Secret Access Key. Keep this secret; do not check into source control.")
            tt.SetToolTip(chkRebuildStartup, "When enabled, the app will attempt to list your S3 bucket on startup and build the local cache to avoid re-uploading existing objects.")
            tt.SetToolTip(chkRebuildSmoketest, "When enabled, the smoketest will attempt to rebuild the local cache from the configured bucket before performing its validation.")

            ' Tab order
            txtFolder.TabIndex = 0
            btnBrowse.TabIndex = 1
            txtBucket.TabIndex = 2
            txtRegion.TabIndex = 3
            txtAccessKey.TabIndex = 4
            txtSecret.TabIndex = 5
            btnEye.TabIndex = 6
            chkRebuildStartup.TabIndex = 7
            chkRebuildSmoketest.TabIndex = 8
            btnOk.TabIndex = 9
            btnCancel.TabIndex = 10

            ' Add to form
            Me.Controls.AddRange(New Control() {grpLocal, grpS3, grpCache, btnOk, btnCancel})
        End Sub
    End Class
End Namespace
