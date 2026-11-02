Imports Newtonsoft.Json
Imports System.IO

Public Class Form4
    ' 引用主窗体的 AppConfig
    Private ReadOnly config As AppConfig

    ' 构造函数：接收 config
    Public Sub New(cfg As AppConfig)
        config = cfg
        InitializeComponent()
    End Sub

    Public Sub New()
    End Sub

    ' ────────── 加载 ──────────
    Private Sub Form4_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 1) 省份下拉：由 TaxSiteHelper 统一填充，保序
        ComboBox1.DropDownStyle = ComboBoxStyle.DropDownList
        ComboBox1.Items.Clear()
        For Each regionName In TaxSiteHelper.GetRegionNames()
            ComboBox1.Items.Add(regionName)
        Next

        ' 2) 把 config 灌进控件
        LoadConfig()
    End Sub

    Private Sub LoadConfig()
        ' 登录区域：先按 config 选中，无效再回落到 "浙江省"
        If Not String.IsNullOrEmpty(config.Region) Then
            ComboBox1.SelectedItem = config.Region
        End If
        If ComboBox1.SelectedIndex < 0 Then
            ComboBox1.SelectedItem = "浙江省"
        End If

        ' AI Key
        TextBox3.Text = config.ZhipuApiKey

        ' 4 组公司 / 店铺
        TextBox4.Text = config.Company1 : TextBox5.Text = config.Shop1
        TextBox7.Text = config.Company2 : TextBox6.Text = config.Shop2
        TextBox9.Text = config.Company3 : TextBox8.Text = config.Shop3
        TextBox11.Text = config.Company4 : TextBox10.Text = config.Shop4

        ' 默认选项
        TextBox12.Text = config.DefaultSpecOption

        ' 关键词（逗号串拆回多框）
        FillKeywords(config.Keywords1, {TextBox13})
        FillKeywords(config.Keywords2, {TextBox15, TextBox27, TextBox28})
        FillKeywords(config.Keywords3, {TextBox17})
        FillKeywords(config.Keywords4, {TextBox24, TextBox29})
        FillKeywords(config.Keywords5, {TextBox22, TextBox30})
        FillKeywords(config.Keywords6, {TextBox20})
        FillKeywords(config.Keywords7, {TextBox26})

        ' 规格列
        TextBox14.Text = config.Spec1
        TextBox16.Text = config.Spec2
        TextBox18.Text = config.Spec3
        TextBox23.Text = config.Spec4
        TextBox21.Text = config.Spec5
        TextBox19.Text = config.Spec6
        TextBox25.Text = config.Spec7

        ' 开票人
        TextBox32.Text = config.Payee
        TextBox31.Text = config.Reviewer
    End Sub

    Private Sub FillKeywords(src As String, boxes() As TextBox)
        Dim parts() As String = If(src, "").Split(","c)
        For i = 0 To boxes.Length - 1
            boxes(i).Text = If(i < parts.Length, parts(i).Trim(), "")
        Next
    End Sub

    ' ────────── 保存（Button10 = "保存"）──────────
    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        If Not SaveConfig() Then Return      ' 校验失败就停

        ' 保存到磁盘（选一种，见第 4 步）
        SaveConfigToFile(config)

        MessageBox.Show("已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ''' <summary>控件 → config。校验失败返回 False</summary>
    Private Function SaveConfig() As Boolean
        ' 省份
        If ComboBox1.SelectedItem Is Nothing Then
            MessageBox.Show("请选择登录区域！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            ComboBox1.Focus()
            Return False
        End If
        config.Region = ComboBox1.SelectedItem.ToString()

        ' AI Key（可选校验）
        config.ZhipuApiKey = TextBox3.Text.Trim()

        ' 4 组公司 / 店铺
        config.Company1 = TextBox4.Text.Trim() : config.Shop1 = TextBox5.Text.Trim()
        config.Company2 = TextBox7.Text.Trim() : config.Shop2 = TextBox6.Text.Trim()
        config.Company3 = TextBox9.Text.Trim() : config.Shop3 = TextBox8.Text.Trim()
        config.Company4 = TextBox11.Text.Trim() : config.Shop4 = TextBox10.Text.Trim()

        ' 默认选项
        config.DefaultSpecOption = TextBox12.Text.Trim()

        ' 关键词
        config.Keywords1 = JoinKeywords({TextBox13})
        config.Keywords2 = JoinKeywords({TextBox15, TextBox27, TextBox28})
        config.Keywords3 = JoinKeywords({TextBox17})
        config.Keywords4 = JoinKeywords({TextBox24, TextBox29})
        config.Keywords5 = JoinKeywords({TextBox22, TextBox30})
        config.Keywords6 = JoinKeywords({TextBox20})
        config.Keywords7 = JoinKeywords({TextBox26})

        ' 规格
        config.Spec1 = TextBox14.Text.Trim()
        config.Spec2 = TextBox16.Text.Trim()
        config.Spec3 = TextBox18.Text.Trim()
        config.Spec4 = TextBox23.Text.Trim()
        config.Spec5 = TextBox21.Text.Trim()
        config.Spec6 = TextBox19.Text.Trim()
        config.Spec7 = TextBox25.Text.Trim()

        ' 开票人
        config.Payee = TextBox32.Text.Trim()
        config.Reviewer = TextBox31.Text.Trim()

        ' ConditionList / TriggerUnit 属于 FormConditionList 管，这里不碰
        Return True
    End Function

    Private Function JoinKeywords(boxes() As TextBox) As String
        Dim list As New List(Of String)
        For Each tb In boxes
            Dim v = tb.Text.Trim()
            If v <> "" Then list.Add(v)
        Next
        Return String.Join(",", list)
    End Function

    ' ────────── 落盘（示例：config.json）──────────
    Private Sub SaveConfigToFile(cfg As AppConfig)
        Dim filePath As String = Path.Combine(Application.StartupPath, "config.json")
        File.WriteAllText(filePath, JsonConvert.SerializeObject(cfg, Formatting.Indented))
    End Sub
End Class