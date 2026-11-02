Public Class LoginInfoForm
    Inherits Form

    Private ReadOnly config As AppConfig
    Private cboAccountSelect As ComboBox   ' 账号选择下拉框
    Private txtTaxNumber As TextBox
    Private txtPhone As TextBox
    Private txtPassword As TextBox
    Private chkSavePassword As CheckBox

    Public Sub New(ByRef appConfig As AppConfig)
        Me.config = appConfig
        InitializeComponent()
        LoadAccountData()
        AddHandler cboAccountSelect.SelectedIndexChanged, AddressOf cboAccountSelect_SelectedIndexChanged
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "税务登录信息设置"
        Me.Size = New Size(420, 300)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False

        ' 账号选择下拉框
        Dim lblSelect As New Label() With {.Text = "选择账号:", .Location = New Point(20, 20), .Size = New Size(100, 30)}
        cboAccountSelect = New ComboBox() With {.Location = New Point(130, 20), .Cursor = Cursors.Hand, .Size = New Size(200, 30), .DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        cboAccountSelect.Items.AddRange(New String() {"税务账号1", "税务账号2", "税务账号3", "税务账号4", "税务账号5"})
        cboAccountSelect.SelectedIndex = 0 ' 恢复上次选中的账号

        ' 纳税人识别号
        Dim lblTax As New Label() With {.Text = "纳税人识别号:", .Location = New Point(20, 60), .Size = New Size(100, 30)}
        txtTaxNumber = New TextBox() With {.Location = New Point(130, 60), .Size = New Size(200, 20), .BorderStyle = BorderStyle.None, .TabStop = False, .AutoSize = False, .Font = New Font("宋体", 10)}

        ' 手机号
        Dim lblPhone As New Label() With {.Text = "手机号:", .Location = New Point(20, 100), .Size = New Size(100, 30)}
        txtPhone = New TextBox() With {.Location = New Point(130, 100), .Size = New Size(200, 20), .BorderStyle = BorderStyle.None, .TabStop = False, .AutoSize = False, .Font = New Font("宋体", 10)}

        ' 密码
        Dim lblPassword As New Label() With {.Text = "密码:", .Location = New Point(20, 140), .Size = New Size(100, 30)}
        txtPassword = New TextBox() With {.Location = New Point(130, 140), .Size = New Size(200, 20), .PasswordChar = "*"c, .BorderStyle = BorderStyle.None, .TabStop = False, .AutoSize = False, .Font = New Font("宋体", 10)}

        ' 保存密码复选框
        chkSavePassword = New CheckBox() With {.Text = "保存密码", .Location = New Point(130, 180), .Cursor = Cursors.Hand, .Size = New Size(100, 20), .FlatStyle = FlatStyle.Flat, .TabStop = False, .AutoSize = False}
        chkSavePassword.FlatAppearance.BorderSize = 0
        ' 按钮
        Dim btnSave As New Button With {
            .Text = "保存", .Location = New Point(130, 220), .Size = New Size(80, 30), .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat,
            .BackColor = SystemColors.ControlLight
        }
        btnSave.FlatAppearance.BorderSize = 0
        AddHandler btnSave.Click, AddressOf BtnSave_Click

        Dim btnCancel As New Button With {
            .Text = "取消", .Location = New Point(220, 220), .Size = New Size(80, 30), .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat,
            .BackColor = SystemColors.ControlLight
        }
        btnCancel.FlatAppearance.BorderSize = 0
        AddHandler btnCancel.Click, AddressOf BtnCancel_Click

        ' 添加控件
        Me.Controls.AddRange(New Control() {lblSelect, cboAccountSelect, lblTax, txtTaxNumber, lblPhone, txtPhone, lblPassword, txtPassword, chkSavePassword, btnSave, btnCancel})
    End Sub

    ' 根据当前选中的账号加载对应的数据
    Private Sub LoadAccountData()
        If config Is Nothing Then Return
        Dim index = cboAccountSelect.SelectedIndex
        Select Case index
            Case 0
                txtTaxNumber.Text = config.TaxNumber1
                txtPhone.Text = config.Phone1
                txtPassword.Text = config.Passwordx1
            Case 1
                txtTaxNumber.Text = config.TaxNumber2
                txtPhone.Text = config.Phone2
                txtPassword.Text = config.Passwordx2
            Case 2
                txtTaxNumber.Text = config.TaxNumber3
                txtPhone.Text = config.Phone3
                txtPassword.Text = config.Passwordx3
            Case 3
                txtTaxNumber.Text = config.TaxNumber4
                txtPhone.Text = config.Phone4
                txtPassword.Text = config.Passwordx4
            Case 4
                txtTaxNumber.Text = config.TaxNumber5
                txtPhone.Text = config.Phone5
                txtPassword.Text = config.Passwordx5
            Case Else
                ' 无效索引，不做处理
                Return
        End Select

        chkSavePassword.Checked = config.SavePassword
        ' 同步更新 config 中的当前账号索引
        config.CurrentTaxAccountIndex = index
        Form1.ComboBox2.SelectedIndex = cboAccountSelect.SelectedIndex
    End Sub

    ' 切换账号时重新加载数据
    Private Sub CboAccountSelect_SelectedIndexChanged(sender As Object, e As EventArgs)
        LoadAccountData()
    End Sub

    ' 保存按钮点击事件
    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        ' 验证输入
        If String.IsNullOrEmpty(txtTaxNumber.Text) Then
            MessageBox.Show("请输入纳税人识别号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtTaxNumber.Focus()
            Return
        End If
        If String.IsNullOrEmpty(txtPhone.Text) Then
            MessageBox.Show("请输入手机号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtPhone.Focus()
            Return
        End If
        If String.IsNullOrEmpty(txtPassword.Text) Then
            MessageBox.Show("请输入密码！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtPassword.Focus()
            Return
        End If

        ' 根据当前选中的账号索引保存到对应的属性
        Dim index = cboAccountSelect.SelectedIndex
        Select Case index
            Case 0
                config.TaxNumber1 = txtTaxNumber.Text
                config.Phone1 = txtPhone.Text
                config.Passwordx1 = txtPassword.Text
            Case 1
                config.TaxNumber2 = txtTaxNumber.Text
                config.Phone2 = txtPhone.Text
                config.Passwordx2 = txtPassword.Text
            Case 2
                config.TaxNumber3 = txtTaxNumber.Text
                config.Phone3 = txtPhone.Text
                config.Passwordx3 = txtPassword.Text
            Case 3
                config.TaxNumber4 = txtTaxNumber.Text
                config.Phone4 = txtPhone.Text
                config.Passwordx4 = txtPassword.Text
            Case 4
                config.TaxNumber5 = txtTaxNumber.Text
                config.Phone5 = txtPhone.Text
                config.Passwordx5 = txtPassword.Text
            Case Else
                MessageBox.Show("无效的账号选择！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
        End Select

        config.SavePassword = chkSavePassword.Checked
        ' 当前账号索引已在 LoadAccountData 中更新，但为了保险再次设置
        config.CurrentTaxAccountIndex = index

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class