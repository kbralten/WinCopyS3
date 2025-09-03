Imports System.Windows.Forms

Namespace WinCopyS3
    Partial Public Class SetupForm
        Inherits Form

        Private ReadOnly _config As AppConfig

        Public Sub New(cfg As AppConfig)
            _config = cfg
            InitializeComponent()

            ' Initialize control values from config
            txtFolder.Text = _config.LocalFolder
            txtBucket.Text = _config.BucketName
            txtRegion.Text = _config.Region
            txtAccessKey.Text = _config.AccessKeyId
            txtSecret.Text = _config.SecretAccessKey
            chkRebuildStartup.Checked = _config.RebuildCacheOnStartup
            chkRebuildSmoketest.Checked = _config.RebuildCacheOnSmoketest

            ' Wire events
            AddHandler btnBrowse.Click, AddressOf BtnBrowse_Click
            AddHandler btnEye.Click, AddressOf BtnEye_Click
            AddHandler Me.FormClosing, AddressOf SetupForm_FormClosing
        End Sub

        Private Sub BtnBrowse_Click(sender As Object, e As EventArgs)
            Using f As New FolderBrowserDialog()
                If f.ShowDialog() = DialogResult.OK Then txtFolder.Text = f.SelectedPath
            End Using
        End Sub

        Private Sub BtnEye_Click(sender As Object, e As EventArgs)
            txtSecret.UseSystemPasswordChar = Not txtSecret.UseSystemPasswordChar
        End Sub

        Private Sub SetupForm_FormClosing(sender As Object, e As FormClosingEventArgs)
            If Me.DialogResult = DialogResult.OK Then
                _config.LocalFolder = txtFolder.Text.Trim()
                _config.BucketName = txtBucket.Text.Trim()
                _config.Region = txtRegion.Text.Trim()
                _config.AccessKeyId = txtAccessKey.Text.Trim()
                _config.SecretAccessKey = txtSecret.Text
                _config.RebuildCacheOnStartup = chkRebuildStartup.Checked
                _config.RebuildCacheOnSmoketest = chkRebuildSmoketest.Checked
                If Not _config.IsConfigured Then
                    MessageBox.Show("Please fill all fields.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    e.Cancel = True
                End If
            End If
        End Sub
    End Class
End Namespace
