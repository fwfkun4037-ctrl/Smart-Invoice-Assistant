Public Class TwoAccountLoginForm
    Inherits Form

    Private ReadOnly config As AppConfig   ' 假设 AppConfig 包含 Account1, Password1, Account2, Password2 属性

    Private cboAccountSelect As ComboBox
    Private txtAccount As TextBox
    Private txtPassword As TextBox
    Private btnSave As Button
    Private btnCancel As Button

    Public Sub New(ByRef appConfig As AppConfig)
        Me.config = appConfig
        InitializeComponent()
        cboAccountSelect.SelectedIndex = config.CurrentAccountIndex
        LoadAccountData()
        AddHandler cboAccountSelect.SelectedIndexChanged, AddressOf cboAccountSelect_SelectedIndexChanged
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "账号登录设置"
        Me.Size = New Size(400, 230)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False

        ' 账号选择下拉框
        Dim lblSelect As New Label() With {.Text = "选择账号:", .Location = New Point(20, 20), .Size = New Size(80, 30)}
        cboAccountSelect = New ComboBox() With {.Location = New Point(110, 20), .Cursor = Cursors.Hand, .Size = New Size(200, 30), .DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        cboAccountSelect.Items.AddRange(New String() {"登录账号1", "登录账号2", "登录账号3", "登录账号4", "登录账号5"})
        cboAccountSelect.SelectedIndex = 0

        ' 账号输入框
        Dim lblAccount As New Label() With {.Text = "账号名:", .Location = New Point(20, 60), .Size = New Size(80, 30)}
        txtAccount = New TextBox() With {.Location = New Point(110, 60), .Size = New Size(200, 20), .BorderStyle = BorderStyle.None, .TabStop = False, .AutoSize = False, .Font = New Font("宋体", 10)}

        ' 密码输入框
        Dim lblPassword As New Label() With {.Text = "密码:", .Location = New Point(20, 100), .Size = New Size(80, 30)}
        txtPassword = New TextBox() With {.Location = New Point(110, 100), .Size = New Size(200, 20), .PasswordChar = "*"c, .BorderStyle = BorderStyle.None, .TabStop = False, .AutoSize = False, .Font = New Font("宋体", 10)}

        ' 按钮
        btnSave = New Button With {
            .Text = "保存", .Location = New Point(110, 150), .Cursor = Cursors.Hand, .Size = New Size(80, 30), .FlatStyle = FlatStyle.Flat,
            .BackColor = SystemColors.ControlLight
        }
        btnSave.FlatAppearance.BorderSize = 0
        AddHandler btnSave.Click, AddressOf BtnSave_Click

        btnCancel = New Button With {
            .Text = "取消", .Location = New Point(200, 150), .Cursor = Cursors.Hand, .Size = New Size(80, 30), .FlatStyle = FlatStyle.Flat,
            .BackColor = SystemColors.ControlLight
        }
        btnCancel.FlatAppearance.BorderSize = 0
        AddHandler btnCancel.Click, AddressOf BtnCancel_Click

        ' 添加控件
        Me.Controls.AddRange(New Control() {lblSelect, cboAccountSelect, lblAccount, txtAccount, lblPassword, txtPassword, btnSave, btnCancel})
    End Sub

    Private Sub LoadAccountData()
        If config Is Nothing Then Return
        ' 根据当前选择的账号索引加载对应的账号和密码
        Select Case cboAccountSelect.SelectedIndex
            Case 0
                txtAccount.Text = config.Account1
                txtPassword.Text = config.Password1
            Case 1
                txtAccount.Text = config.Account2
                txtPassword.Text = config.Password2
            Case 2
                txtAccount.Text = config.Account3
                txtPassword.Text = config.Password3
            Case 3
                txtAccount.Text = config.Account4
                txtPassword.Text = config.Password4
            Case 4
                txtAccount.Text = config.Account5
                txtPassword.Text = config.Password5
            Case Else
                ' 如果索引超出范围，不做处理
                Return
        End Select

        config.CurrentAccountIndex = cboAccountSelect.SelectedIndex
        Form1.ComboBox1.SelectedIndex = cboAccountSelect.SelectedIndex
    End Sub

    Private Sub CboAccountSelect_SelectedIndexChanged(sender As Object, e As EventArgs)
        LoadAccountData()
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        ' 验证输入
        If String.IsNullOrEmpty(txtAccount.Text) Then
            MessageBox.Show("请输入账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtAccount.Focus()
            Return
        End If

        If String.IsNullOrEmpty(txtPassword.Text) Then
            MessageBox.Show("请输入密码！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtPassword.Focus()
            Return
        End If

        ' 根据当前选中的账号索引保存到 config 对应的属性
        Select Case cboAccountSelect.SelectedIndex
            Case 0
                config.Account1 = txtAccount.Text
                config.Password1 = txtPassword.Text
            Case 1
                config.Account2 = txtAccount.Text
                config.Password2 = txtPassword.Text
            Case 2
                config.Account3 = txtAccount.Text
                config.Password3 = txtPassword.Text
            Case 3
                config.Account4 = txtAccount.Text
                config.Password4 = txtPassword.Text
            Case 4
                config.Account5 = txtAccount.Text
                config.Password5 = txtPassword.Text
            Case Else
                MessageBox.Show("无效的账号选择！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
        End Select

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class