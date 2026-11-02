Imports System.Windows.Documents

Public Class Form3
    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 图标
        Dim picInfo As New PictureBox With {
        .Image = SystemIcons.Question.ToBitmap(),
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
            "使用说明：" & vbCrLf & vbCrLf &
            "1. 左侧是浙江税务开票网站" & vbCrLf &
            "2. 右侧是淘宝开票信息页面" & vbCrLf &
            "3. 点击'税务操作'菜单保存登录信息" & vbCrLf &
            "4. 点击'填充登录信息'自动填充登录信息" & vbCrLf &
            "5. 在右侧保存淘宝账号并进入开票信息页面" & vbCrLf &
            "6. 点击'智能识别开票信息'自动提取信息" & vbCrLf &
            "7. 或在下方手动输入开票信息（支持智能解析）" & vbCrLf &
            "8. 点击'智能填入详情'将信息填入税务网站" & vbCrLf &
            "9. 支持订单号输入，合并多订单请增加开票行" & vbCrLf & vbCrLf &
            "10. 点击千牛开票详情或者输入订单号开票" & vbCrLf &
            "    快捷键F1--->F2--->F3" & vbCrLf &
            "网站操作说明：" & vbCrLf &
            "刷新网站：重新加载当前网页" & vbCrLf &
            "如有疑问请联系：QQ：653474217" & vbCrLf & vbCrLf &
            "支持一下开发者，感谢您的打赏！"
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