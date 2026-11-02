Imports System.Windows.Documents

Public Class Form2
    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 图标
        Dim picInfo As New PictureBox With {
        .Image = SystemIcons.Information.ToBitmap(),
        .SizeMode = PictureBoxSizeMode.AutoSize,
        .Location = New Point(15, 15)
    }
        Me.Controls.Add(picInfo)
        picInfo.BringToFront()
        RichTextBox1.ReadOnly = True
        RichTextBox1.BorderStyle = BorderStyle.None
        RichTextBox1.BackColor = Me.BackColor
        RichTextBox1.ScrollBars = RichTextBoxScrollBars.None
        RichTextBox1.Text =
            "智能开票助手 v8.0" & vbCrLf &
            "功能特色：" & vbCrLf &
            "1. 自动识别并填充登录信息" & vbCrLf &
            "2. 智能识别淘宝开票信息" & vbCrLf &
            "3. 智能解析手动输入的开票信息" & vbCrLf &
            "4. 自动保存登录信息和配置" & vbCrLf &
            "5. 使用微软 WebView2 内核，性能更好" & vbCrLf & vbCrLf &
            "网页 JavaScript更新日期：2026年5月" & vbCrLf & vbCrLf &
            "日期：" & DateTime.Now.ToString("yyyy-MM-dd")
        ' 设置行距（单位：磅，默认约 12）
        RichTextBox1.SelectionStart = 0
        RichTextBox1.SelectionLength = RichTextBox1.TextLength
        RichTextBox1.SelectionCharOffset = 0
        Dim p As New Paragraph With {
            .LineHeight = 20      ' 想要多大填多大
            }
        RichTextBox1.SelectionFont = New Font(RichTextBox1.Font, FontStyle.Regular)
    End Sub
    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        Me.Close()
    End Sub
End Class