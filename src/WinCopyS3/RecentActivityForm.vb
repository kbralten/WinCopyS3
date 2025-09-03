Imports System.Windows.Forms

Namespace WinCopyS3
    Public Class RecentActivityForm
        Inherits Form

        Public Sub New(logger As InMemoryLogger)
            Me.Text = "Recent Activity"
            Me.Width = 520
            Me.Height = 320
            Me.StartPosition = FormStartPosition.CenterScreen

            Dim lb = New ListBox() With {.Dock = DockStyle.Fill}
            lb.Items.AddRange(logger.Recent.ToArray())
            Me.Controls.Add(lb)
        End Sub
    End Class
End Namespace
