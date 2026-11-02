Imports System.IO
Imports System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar
Imports Newtonsoft.Json

Public Class FormConditionList
    Inherits Form

    Private ReadOnly config As AppConfig
    Private lstConditions As ListBox
    Private txtNewCondition As TextBox
    Private txtUnit As TextBox
    Private btnAdd As Button
    Private btnRemove As Button
    Private btnSave As Button
    Private btnClose As Button

    Public Sub New(ByRef appConfig As AppConfig)
        Me.config = appConfig
        InitializeComponent()
        LoadConditionList()
        LoadUnit()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "管理触发规格"
        Me.ClientSize = New Size(290, 350)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False

        ' ── 触发单位（原写死的"件"改为可编辑）──
        Dim lblUnit As New Label With {
            .Text = "触发单位:",
            .Location = New Point(20, 18),
            .Size = New Size(70, 20),
            .Font = New Font("宋体", 10)
        }
        txtUnit = New TextBox With {
            .Location = New Point(95, 14),
            .Size = New Size(80, 22),
            .Font = New Font("宋体", 10),
            .MaxLength = 10
        }

        ' ── 说明标签 ──
        Dim lblTip As New Label With {
            .Text = "以下规格会触发上述单位：",
            .Location = New Point(20, 45),
            .Size = New Size(250, 20),
            .Font = New Font("宋体", 10)
        }

        ' ── 规格列表 ──
        lstConditions = New ListBox With {
            .Location = New Point(20, 70),
            .Size = New Size(250, 180),
            .BorderStyle = BorderStyle.FixedSingle,
            .Font = New Font("宋体", 10),
            .IntegralHeight = False
        }

        ' ── 新增规格 ──
        Dim lblNew As New Label With {
            .Text = "增加规格名称:",
            .Location = New Point(20, 262),
            .Size = New Size(90, 20),
            .Font = New Font("宋体", 10)
        }
        txtNewCondition = New TextBox With {
            .Location = New Point(115, 259),
            .Size = New Size(155, 22),
            .Font = New Font("宋体", 10)
        }

        ' ── 按钮 ──
        btnAdd = MakeButton("添加", 20)
        AddHandler btnAdd.Click, AddressOf BtnAdd_Click
        btnRemove = MakeButton("删除", 85)
        AddHandler btnRemove.Click, AddressOf BtnRemove_Click
        btnSave = MakeButton("保存", 150)
        AddHandler btnSave.Click, AddressOf BtnSave_Click
        btnClose = MakeButton("取消", 215)
        AddHandler btnClose.Click, AddressOf BtnClose_Click

        Me.Controls.AddRange(New Control() {
            lblUnit, txtUnit, lblTip, lstConditions,
            lblNew, txtNewCondition,
            btnAdd, btnRemove, btnSave, btnClose
        })
    End Sub

    ' 按钮统一样式，省去四段重复代码
    Private Function MakeButton(text As String, x As Integer) As Button
        Dim b As New Button With {
            .Text = text,
            .Location = New Point(x, 295),
            .Size = New Size(55, 30),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .BackColor = SystemColors.ControlLight
        }
        b.FlatAppearance.BorderSize = 0
        Return b
    End Function

    ' ── 单位读取/写入 ──
    Private Sub LoadUnit()
        txtUnit.Text = If(String.IsNullOrWhiteSpace(config?.TriggerUnit), "件", config.TriggerUnit)
    End Sub

    ' ── 规格列表 ──
    Private Sub LoadConditionList()
        If config Is Nothing Then Return
        lstConditions.DataSource = Nothing
        lstConditions.DataSource = config.ConditionList
        lstConditions.SelectedIndex = -1
    End Sub

    Private Sub BtnAdd_Click(sender As Object, e As EventArgs)
        Dim newItem = txtNewCondition.Text.Trim()
        If String.IsNullOrEmpty(newItem) Then
            MessageBox.Show("请输入规格名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtNewCondition.Focus()
            Return
        End If

        If config.ConditionList.Contains(newItem) Then
            MessageBox.Show("该规格已存在！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtNewCondition.Clear()
            txtNewCondition.Focus()
            Return
        End If

        config.ConditionList.Add(newItem)
        LoadConditionList()
        txtNewCondition.Clear()
        txtNewCondition.Focus()
    End Sub

    Private Sub BtnRemove_Click(sender As Object, e As EventArgs)
        If lstConditions.SelectedIndex = -1 Then
            MessageBox.Show("请先选中要删除的规格！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim selectedItem = lstConditions.SelectedItem.ToString()
        config.ConditionList.Remove(selectedItem)
        LoadConditionList()
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        ' 校验并保存单位
        Dim unit As String = txtUnit.Text.Trim()
        If String.IsNullOrEmpty(unit) Then
            MessageBox.Show("请输入触发单位！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtUnit.Focus()
            Return
        End If
        config.TriggerUnit = unit

        ' 文本框里还剩新规格就一并加入
        Dim newItem = txtNewCondition.Text.Trim()
        If newItem <> "" AndAlso Not config.ConditionList.Contains(newItem) Then
            config.ConditionList.Add(newItem)
        End If

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class