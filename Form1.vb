Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Runtime.Remoting
Imports System.Security
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Web
Imports System.Windows
Imports Microsoft.Web.WebView2
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports ZXing
Imports System.NativeTray
Public Class Form1
    ' 声明托盘图标宿主
    Private WithEvents TrayIcon As TrayIconHost
    Private isExiting As Boolean = False
    Private savedWindowState As FormWindowState = FormWindowState.Normal
    ' 声明 Windows API
    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Private Shared Function FindWindow(lpClassName As String, lpWindowName As String) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function
    Private WithEvents WebViewTaoBao As WebView2
    Private WithEvents WebViewTax As WebView2
    Private webViewx As WebView2
    Private buyerName As String = ""
    Private isProcessingLogin As Boolean = False
    Private isFrameHandlerAdded As Boolean = False
    Private taobaoLoginFrame As Microsoft.Web.WebView2.Core.CoreWebView2Frame = Nothing
    Private downloadTcs As TaskCompletionSource(Of String)

    Private invoiceData As New Dictionary(Of String, String)()
    ' 配置对象
    Private config As New AppConfig()
    Private configPath As String = Path.Combine(System.Windows.Forms.Application.StartupPath, "config.json")
    Private b23TipShown As Boolean = False
    ' WebView2 环境
    Private env As CoreWebView2Environment
    ' 专门用于等待 API 拦截结果的任务源
    Private _apiDataTcs As TaskCompletionSource(Of JObject)
    Private hiddenWebView As WebView2
    Private extractionTcs As TaskCompletionSource(Of JObject)
    Private isScriptExecuted As Boolean = False
    Private shangjiang As String = "1"

    Private isB2BMode As Boolean = False   ' 1688模式开关
    ' ===== 1688开票信息（拦截发票接口获取）=====
    Private _b2bSumPayment As String = "0.00"
    Private _b2bInvoiceTitle As String = ""     ' 公司名称（发票抬头）
    Private _b2bTaxNo As String = ""            ' 公司税号
    Private _b2bRegAddress As String = ""       ' 注册地址
    Private _b2bBankName As String = ""         ' 开户银行
    Private _b2bBankAccount As String = ""      ' 银行账号
    Private _b2bRegPhone As String = ""         ' 注册电话
    Private _b2bCurrentOrderId As String = ""
    Private _invoiceTcs As TaskCompletionSource(Of Boolean) = Nothing

    Private taxPageLoaded As Boolean = False
    Private monitorTask As Task = Nothing
    Private cancellationSource As CancellationTokenSource = Nothing  ' 剪贴板内容的哈希值，用于检测变化
    Private lastProcessedText As String = ""   ' 记录上一次处理的文本，避免重复打开

    ' 模块级缓存变量
    Private _invoiceData As String = String.Empty
    Private _result As String = String.Empty
    '最小化时不触发按键
    ' API声明 - 修复所有缺失的声明
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Sub keybd_event(bVk As Byte, bScan As Byte, dwFlags As Integer, dwExtraInfo As IntPtr)
    End Sub
    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetAsyncKeyState(ByVal vKey As Keys) As Short
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function SetWindowsHookEx(ByVal idHook As Integer,
                                            ByVal lpfn As LowLevelKeyboardProcDelegate,
                                            ByVal hMod As IntPtr,
                                            ByVal dwThreadId As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function UnhookWindowsHookEx(ByVal hhk As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function CallNextHookEx(ByVal hhk As IntPtr,
                                           ByVal nCode As Integer,
                                           ByVal wParam As IntPtr,
                                           ByVal lParam As IntPtr) As IntPtr
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetModuleHandle(ByVal lpModuleName As String) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function FindWindowEx(hwndParent As IntPtr, hwndChildAfter As IntPtr, lpszClass As String, lpszWindow As String) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As UInteger, wParam As IntPtr, lParam As String) As IntPtr
    End Function

    ' 常量定义
    Private Const WH_KEYBOARD_LL As Integer = 13
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_SYSKEYDOWN As Integer = &H104
    ' 键盘常量
    Private Const KEYEVENTF_KEYDOWN As Integer = &H0
    Private Const KEYEVENTF_KEYUP As Integer = &H2

    ' 按键码
    Private Const VK_CONTROL As Byte = &H11
    Private Const VK_V As Byte = &H56
    Private Const VK_RETURN As Byte = &HD    ' 键盘常量


    ' 委托定义 - 使用唯一名称避免冲突
    Private Delegate Function LowLevelKeyboardProcDelegate(
        ByVal nCode As Integer,
        ByVal wParam As IntPtr,
        ByVal lParam As IntPtr) As IntPtr

    ' 钩子句柄
    Private hookID As IntPtr = IntPtr.Zero

    ' 钩子回调函数的委托实例
    Private hookDelegate As LowLevelKeyboardProcDelegate

    ' 键盘钩子回调函数
    Private Function KeyboardHookProc(ByVal nCode As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
        ' 检查是否需要处理此消息
        If nCode >= 0 Then
            ' 检查是否是按键按下事件
            If wParam = WM_KEYDOWN OrElse wParam = WM_SYSKEYDOWN Then
                ' 读取按键码
                Dim vkCode As Integer = Marshal.ReadInt32(lParam)

                ' 检查Alt键是否按下（用于区分纯上下箭头）
                Dim altPressed As Boolean = (GetAsyncKeyState(Keys.Menu) And &H8000) <> 0

                ' ========== F1～F4 功能键映射（全部阻止默认行为） ==========
                ' F1 → Button8
                If vkCode = CInt(Keys.F1) Then
                    If Me.InvokeRequired Then
                        Me.Invoke(Sub()
                                      If Me.WindowState <> FormWindowState.Minimized AndAlso
                           Button8.Enabled AndAlso Button8.Visible Then
                                          Button8.PerformClick()
                                      End If
                                  End Sub)
                    Else
                        If Me.WindowState <> FormWindowState.Minimized AndAlso
                       Button8.Enabled AndAlso Button8.Visible Then
                            Button8.PerformClick()
                        End If
                    End If
                    ' 阻止 F1 默认行为（如打开帮助）
                    Return New IntPtr(1)
                End If

                ' F2 → Button22
                If vkCode = CInt(Keys.F2) Then
                    If Me.InvokeRequired Then
                        Me.Invoke(Sub()
                                      If Me.WindowState <> FormWindowState.Minimized AndAlso
                           Button22.Enabled AndAlso Button22.Visible Then
                                          Button22.PerformClick()
                                      End If
                                  End Sub)
                    Else
                        If Me.WindowState <> FormWindowState.Minimized AndAlso
                       Button22.Enabled AndAlso Button22.Visible Then
                            Button22.PerformClick()
                        End If
                    End If
                    ' 阻止 F2 默认行为（如重命名）
                    Return New IntPtr(1)
                End If

                ' F3 → Button15
                If vkCode = CInt(Keys.F3) Then
                    If Me.InvokeRequired Then
                        Me.Invoke(Sub()
                                      If Me.WindowState <> FormWindowState.Minimized AndAlso
                           Button15.Enabled AndAlso Button15.Visible Then
                                          Button15.PerformClick()
                                      End If
                                  End Sub)
                    Else
                        If Me.WindowState <> FormWindowState.Minimized AndAlso
                       Button15.Enabled AndAlso Button15.Visible Then
                            Button15.PerformClick()
                        End If
                    End If
                    ' 阻止 F3 默认行为（如网页查找）
                    Return New IntPtr(1)
                End If

                ' F4 → Button7（原 Alt+W）
                If vkCode = CInt(Keys.F4) Then
                    If Me.InvokeRequired Then
                        Me.Invoke(Sub()
                                      If Me.WindowState <> FormWindowState.Minimized AndAlso
                           Button7.Enabled AndAlso Button7.Visible Then
                                          Button7.PerformClick()
                                      End If
                                  End Sub)
                    Else
                        If Me.WindowState <> FormWindowState.Minimized AndAlso
                       Button7.Enabled AndAlso Button7.Visible Then
                            Button7.PerformClick()
                        End If
                    End If
                    ' 阻止 F4 默认行为（如关闭窗口，但这里仅作示例）
                    Return New IntPtr(1)
                End If

                ' 处理纯按键（无 Alt 修饰符）的上下箭头
                If Not altPressed Then
                    ' 增加：上箭头
                    If vkCode = CInt(Keys.Up) Then
                        If NumericUpDown1.Value < NumericUpDown1.Maximum Then
                            NumericUpDown1.Value += NumericUpDown1.Increment
                        End If
                        Return New IntPtr(1)
                    End If

                    ' 减少：下箭头
                    If vkCode = CInt(Keys.Down) Then
                        If NumericUpDown1.Value > NumericUpDown1.Minimum Then
                            NumericUpDown1.Value -= NumericUpDown1.Increment
                        End If
                        Return New IntPtr(1)
                    End If
                End If
            End If
        End If

        ' 传递给下一个钩子
        Return CallNextHookEx(hookID, nCode, wParam, lParam)
    End Function

    ' 安装键盘钩子
    Private Sub InstallKeyboardHook()
        If hookID = IntPtr.Zero Then
            ' 创建委托实例
            hookDelegate = New LowLevelKeyboardProcDelegate(AddressOf KeyboardHookProc)

            ' 获取当前进程模块句柄
            Using curProcess As Process = Process.GetCurrentProcess()
                Using curModule As ProcessModule = curProcess.MainModule
                    ' 使用模块名获取模块句柄
                    hookID = SetWindowsHookEx(WH_KEYBOARD_LL,
                                             hookDelegate,
                                             GetModuleHandle(curModule.ModuleName),
                                             0)
                End Using
            End Using

            ' 检查钩子是否安装成功
            If hookID = IntPtr.Zero Then
                Dim errorCode As Integer = Marshal.GetLastWin32Error()
                MessageBox.Show($"安装键盘钩子失败，错误代码: {errorCode}")
            End If
        End If
    End Sub

    ' 卸载键盘钩子
    Private Sub UninstallKeyboardHook()
        If hookID <> IntPtr.Zero Then
            Dim result As Boolean = UnhookWindowsHookEx(hookID)
            hookID = IntPtr.Zero
            hookDelegate = Nothing

            If Not result Then
                Dim errorCode As Integer = Marshal.GetLastWin32Error()
                MessageBox.Show($"卸载键盘钩子失败，错误代码: {errorCode}")
            End If
        End If
    End Sub

    ' 窗体加载事件
    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 安装键盘钩子
        InstallKeyboardHook()
    End Sub

    ' 窗体关闭事件
    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If Not isExiting AndAlso e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            If Me.WindowState = FormWindowState.Maximized Then
                Me.WindowState = FormWindowState.Normal
            End If
            ' 保存当前窗口状态
            savedWindowState = Me.WindowState
            Me.Hide()

            '' NativeTray 气泡通知
            'trayIcon.ShowBalloonTip(
            '    3000,
            '    "提示",
            '    "程序已最小化到托盘，双击图标可还原",
            '    TrayToolTipIcon.Info
            ')
        Else
            Dim index = ComboBox1.SelectedIndex
            config.CurrentAccountIndex = index
            Dim index2 = ComboBox1.SelectedIndex
            config.CurrentTaxAccountIndex = index2
            SaveConfig()
            ' 卸载键盘钩子
            UninstallKeyboardHook()
            cancellationSource?.Cancel()
            ' 等待最多 1 秒，避免强制关闭时引发异常
            monitorTask?.Wait(1000)
            webViewx?.Dispose()
        End If
    End Sub

    ' 窗体大小改变事件
    Private Sub MainForm_Resize(sender As Object, e As EventArgs) Handles MyBase.Resize
        ' 可以在这里添加日志或状态更新
        Select Case Me.WindowState
            Case FormWindowState.Minimized
                ' 窗体最小化时的操作
                ' lblStatus.Text = "窗体已最小化 - 快捷键已禁用"
            Case FormWindowState.Normal
                ' 窗体正常显示时的操作
                ' lblStatus.Text = "窗体正常显示 - 快捷键已启用"
        End Select
    End Sub
    ' 使用Windows API模拟粘贴
    Public Shared Sub SendPaste()
        ' 按下Ctrl
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero)
        Thread.Sleep(10)

        ' 按下V
        keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero)
        Thread.Sleep(10)

        ' 释放V
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, IntPtr.Zero)
        Thread.Sleep(10)

        ' 释放Ctrl
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero)
    End Sub

    ' 使用Windows API模拟回车
    Public Shared Sub SendEnter()
        ' 按下回车
        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero)
        Thread.Sleep(10)

        ' 释放回车
        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, IntPtr.Zero)
    End Sub


    ''' <summary>鼠标在父容器上移动时，检查是否落在禁用的Button23上</summary>
    Private Sub Parent_MouseMove(sender As Object, e As MouseEventArgs)
        ' 转换成Button23的父容器坐标再判断（坐标系一致才准确）
        Dim p As System.Drawing.Point = Button23.Parent.PointToClient(Cursor.Position)
        Dim r As New System.Drawing.Rectangle(Button23.Location, Button23.Size)

        If r.Contains(p) AndAlso Not Button23.Enabled Then
            If Not b23TipShown Then
                b23TipShown = True
                ToolTip1.Show("需先退出当前账号登录，才能切换平台",
                          Button23.Parent,
                          Button23.Location.X - 25,
                          Button23.Location.Y + Button23.Height + 8,
                          2000)
            End If
        Else
            b23TipShown = False
            ToolTip1.Hide(Button23.Parent)
        End If
    End Sub


    ''' <summary>悬停片刻后兜底再触发一次（防止移动太快跳过MouseMove）</summary>
    Private Sub Parent_MouseHover(sender As Object, e As EventArgs)
        Parent_MouseMove(Nothing, Nothing)
    End Sub



    ' 窗体加载事件================================================
    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ExtractWebView2Loader()
        Await InitializeWebView2Environment()
        LoadConfig()
        Await InitializeBrowsers()
        ' 恢复两个浏览器的登录状态
        isB2BMode = config.IsB2BMode
        If isB2BMode Then
            shangjiang = "2"
            Await RestoreLoginState(WebViewTaoBao, config.B2BCookies, "1688")
        Else
            Await RestoreLoginState(WebViewTaoBao, config.TaoBaoCookies, "淘宝")
        End If
        UpdateB2BButton()

        Await RestoreLoginState(WebViewTax, config.TaxCookies, "税务")
        ' 等待两个 WebView2 完成初始化
        Await WebViewTax.EnsureCoreWebView2Async(Nothing)
        Await WebViewTaoBao.EnsureCoreWebView2Async(Nothing)
        Await SetAutoZoom(WebViewTaoBao)
        Await SetAutoZoom(WebViewTax)
        Await InitWebView2Async()
        SetupEventHandlers()
        LoadTaxWebPage()
        Await ClickLoginButton()
        LoadTaoBaoWebPage()
        SplitContainer1.SplitterDistance = 750 '100-650
        TextBox2.Text = config.SelectedPath
        ComboBox1.Items.Add(config.Shop1)
        ComboBox1.Items.Add(config.Shop2)
        ComboBox1.Items.Add(config.Shop3)
        ComboBox1.Items.Add(config.Shop4)
        ComboBox1.Items.Add("备用")
        ComboBox2.Items.Add(config.Shop1)
        ComboBox2.Items.Add(config.Shop2)
        ComboBox2.Items.Add(config.Shop3)
        ComboBox2.Items.Add(config.Shop4)
        ComboBox2.Items.Add("备用")
        ComboBox1.SelectedIndex = config.CurrentAccountIndex
        ComboBox2.SelectedIndex = config.CurrentTaxAccountIndex
        InitDownloadSystem()
        ' 等待WebView2加载完成
        Await WebViewTaoBao.EnsureCoreWebView2Async()
        ConfigureTextBox()
        NumericUpDown1.Minimum = 0
        NumericUpDown1.Maximum = 20
        NumericUpDown1.Increment = 1
        TextBox3.AutoSize = False
        TextBox3.Height = 16
        TextBox3.TextAlign = HorizontalAlignment.Left
        ' 设置窗体最大化
        HideWebViewScrollbar(WebViewTaoBao)
        HideWebViewScrollbar(WebViewTax)
        CheckBox1.Checked = config.SaveDrawer
        'Me.WindowState = FormWindowState.Maximized
        AddHandler Button23.Parent.MouseMove, AddressOf Parent_MouseMove
        AddHandler Button23.Parent.MouseHover, AddressOf Parent_MouseHover

        ' 1. 创建托盘图标宿主
        TrayIcon = New TrayIconHost() With {
            .ToolTipText = "智能开票助手",
            .Icon = Me.Icon.Handle ' 使用窗体图标；也可用 SystemIcons.Application.Handle
        }

        ' 2. 创建右键菜单
        Dim menu As New TrayMenu()

        ' 还原菜单项
        Dim restoreItem As New TrayMenuItem() With {
            .Header = "显示窗口",
            .Command = New TrayCommand(Sub(param) ShowWindow())
        }
        menu.Add(restoreItem)

        ' 分隔线（可选）
        menu.Add(New TraySeparator())

        ' 退出菜单项
        Dim exitItem As New TrayMenuItem() With {
            .Header = "退出程序",
            .Command = New TrayCommand(Sub(param) ExitApp())
        }
        menu.Add(exitItem)

        ' 3. 将菜单绑定到托盘图标
        trayIcon.Menu = menu

        ' 4. 双击托盘图标还原窗口
        AddHandler trayIcon.LeftDoubleClick, Sub(sender2, e2) ShowWindow()
    End Sub

    ' 还原窗口（顺序很重要，避免闪烁）
    Private Sub ShowWindow()
        Me.Show()
        ' 恢复隐藏前的窗口状态
        If savedWindowState = FormWindowState.Maximized Then
            Me.WindowState = FormWindowState.Maximized
        Else
            Me.WindowState = FormWindowState.Normal
        End If
        Me.Activate()
    End Sub

    ' 退出程序
    Private Sub ExitApp()
        isExiting = True
        Me.Close()
    End Sub
    ' 1. 初始化入口
    Private Async Sub InitDownloadSystem()
        Try
            ' 确保内核加载
            Await WebViewTax.EnsureCoreWebView2Async()

            ' 绑定主窗口下载事件
            AddHandler WebViewTax.CoreWebView2.DownloadStarting, AddressOf HandleDownload

            ' 绑定新窗口请求（这是你之前失败的关键）
            AddHandler WebViewTax.CoreWebView2.NewWindowRequested, AddressOf HandleNewWindow

            lblStatus.Text = "下载拦截已就绪"
        Catch ex As Exception
            ' 这里能看到 TargetInvocationException 内部的真实原因
            lblStatus.Text = "初始化失败: " & ex.ToString()
        End Try
    End Sub

    ' 2. 处理弹窗：强制让弹窗“借用”主窗口的内核设置
    Private Sub HandleNewWindow(sender As Object, e As Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs)
        ' 获取延迟对象，防止异步冲突
        Dim deferral = e.GetDeferral()

        ' 核心策略：不允许产生新窗口，强制在当前 webViewTax 中加载下载链接
        ' 这样它就必然会触发我们已经在 webViewTax 上绑定的 HandleDownload 事件
        e.Handled = True
        WebViewTax.CoreWebView2.Navigate(e.Uri)

        deferral.Complete()
    End Sub

    ' 3. 核心下载逻辑（手动强制路径）
    Private Sub HandleDownload(sender As Object, e As Microsoft.Web.WebView2.Core.CoreWebView2DownloadStartingEventArgs)
        Try
            ' 实时获取 TextBox2 的路径
            Dim myPath As String = TextBox2.Text.Trim()

            ' 1. 确保目录存在
            If Not System.IO.Directory.Exists(myPath) Then
                System.IO.Directory.CreateDirectory(myPath)
            End If

            ' 2. 获取文件名 (适配所有版本的写法)
            Dim fileName As String = ""
            Try
                ' 尝试从 DownloadOperation 获取
                fileName = System.IO.Path.GetFileName(e.DownloadOperation.ResultFilePath)
            Catch
                ' 保底：如果报错，从 URL 提取
                fileName = "file_" & DateTime.Now.ToString("HHmmss") & ".pdf"
            End Try

            ' 3. 强制改写保存位置
            e.ResultFilePath = System.IO.Path.Combine(myPath, fileName)

            ' 4. 关键：这一行必须执行，才能跳过系统的另存为窗口
            e.Handled = True

            Me.Invoke(Sub() lblStatus.Text = "文件已保存")
        Catch ex As Exception
            ' 静默处理或记录日志
        End Try
    End Sub

    ' 初始化 WebView2 环境
    Private Async Function InitializeWebView2Environment() As Task
        Try
            Dim userDataFolder = Path.Combine(System.Windows.Forms.Application.StartupPath, "WebView2Data")
            env = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
            lblStatus.Text = "WebView2 环境初始化完成"
        Catch ex As Exception
            lblStatus.Text = "WebView2 初始化失败: {ex.Message}"
        End Try
    End Function
    '初始化隐藏 WebView（只做一次）
    Private Async Function InitHiddenWebView() As Task
        If hiddenWebView IsNot Nothing Then Return

        hiddenWebView = New WebView2 With {
            .Width = 1,
            .Height = 1,
            .Visible = True
        }
        Me.Controls.Add(hiddenWebView)

        ' ⭐ 关键：与主 WebView 共用用户数据目录
        Dim userDataFolder As String =
        WebViewTaoBao.CoreWebView2.Environment.UserDataFolder

        Dim env = Await CoreWebView2Environment.CreateAsync(
        Nothing,
        userDataFolder
    )

        Await hiddenWebView.EnsureCoreWebView2Async(env)
    End Function

    ' 定义两个不同的后台窗口  logicWebView
    Private dataWebView As WebView2
    Private logicWebView As WebView2
    '释放"WebView2Loader.dll"
    Private Sub ExtractWebView2Loader()
        Dim dllPath As String = Path.Combine(System.Windows.Forms.Application.StartupPath, "WebView2Loader.dll")
        If Not File.Exists(dllPath) Then
            ' 先列出所有资源名确认
            Dim names = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            Dim resourceName As String = names.FirstOrDefault(Function(n) n.EndsWith("WebView2Loader.dll"))
            If resourceName IsNot Nothing Then
                Using stream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                    Dim bytes(stream.Length - 1) As Byte
                    stream.Read(bytes, 0, bytes.Length)
                    File.WriteAllBytes(dllPath, bytes)
                End Using
            End If
        End If
    End Sub
    ' 通用的创建函数
    Private Async Function PrepareHiddenWebViews() As Task
        If dataWebView Is Nothing OrElse dataWebView.IsDisposed Then
            dataWebView = New WebView2 With {
                .Size = New System.Drawing.Size(1280, 900),
                .Location = New System.Drawing.Point(-3000, -3000)
            }
            Me.Controls.Add(dataWebView)
            dataWebView.Visible = True
            Await dataWebView.EnsureCoreWebView2Async(env)

            AddHandler dataWebView.CoreWebView2.WebResourceResponseReceived, AddressOf OnQianNiuResponseReceived
        End If
    End Function
    Private Async Function InitHiddenWebView2() As Task
        If logicWebView IsNot Nothing Then Return

        logicWebView = New WebView2 With {
            .Width = 1,
            .Height = 1,
            .Visible = True
        }
        Me.Controls.Add(logicWebView)

        ' ⭐ 关键：与主 WebView 共用用户数据目录
        Dim userDataFolder As String =
        WebViewTaoBao.CoreWebView2.Environment.UserDataFolder

        Dim env = Await CoreWebView2Environment.CreateAsync(
        Nothing,
        userDataFolder
    )

        Await logicWebView.EnsureCoreWebView2Async(env)
    End Function
    Private Async Sub OnQianNiuResponseReceived(sender As Object, e As CoreWebView2WebResourceResponseReceivedEventArgs)
        ' 【诊断】凡是发票相关请求都记一笔
        If e.Request.Uri.Contains("kingsuns") Then
        End If

        ' ===== dataline 拦截分支（原有代码不动）=====
        If e.Request.Uri.Contains("mtop.1688.kingsuns.invoice.dataline.service") Then
            Try
                If e.Response Is Nothing Then
                ElseIf e.Response.StatusCode <> 200 Then
                Else
                    Dim content = Await e.Response.GetContentAsync()
                    Dim raw As String
                    Using reader As New IO.StreamReader(content)
                        raw = Await reader.ReadToEndAsync()
                    End Using
                    ParseB2bInvoice(raw)
                End If
            Catch ex As Exception
                lblStatus.Text = "dataline读取异常: " & ex.Message
            End Try
            Return
        End If
        Dim targetKey As String
        If isB2BMode Then
            targetKey = "mtoporderservice.queryorder"          ' ← 1688
        ElseIf shangjiang = "0" Then
            targetKey = "orderdetailqianniu"                   ' 天猫
        Else
            targetKey = "trade_order_detail_qian_niu"          ' 淘宝
        End If

        If e.Request.Uri.ToLower().Contains(targetKey) Then
            Try
                If e.Response IsNot Nothing AndAlso e.Response.StatusCode = 200 Then
                    Dim content = Await e.Response.GetContentAsync()
                    If content IsNot Nothing Then
                        Using reader As New IO.StreamReader(content)
                            Dim rawJson = Await reader.ReadToEndAsync()

                            ' 兼容 JSONP 包裹：mtopjsonp3({...}) → {...}
                            Dim p1 = rawJson.IndexOf("("c), p2 = rawJson.LastIndexOf(")"c)
                            If rawJson.StartsWith("mtopjsonp") AndAlso p1 >= 0 AndAlso p2 > p1 Then
                                rawJson = rawJson.Substring(p1 + 1, p2 - p1 - 1)
                            End If

                            Dim jo = JObject.Parse(rawJson)
                            _apiDataTcs?.TrySetResult(jo)
                        End Using
                    End If
                End If
            Catch ex As Exception
                Debug.WriteLine("解析响应 JSON 失败: " & ex.Message)
            End Try
        End If
    End Sub
    Private Sub ParseB2bInvoice(json As String)
        Try
            ' JSONP 包裹兼容：mtopjsonp5({...}) → {...}
            Dim p1 = json.IndexOf("("c), p2 = json.LastIndexOf(")"c)
            If json.StartsWith("mtopjsonp") AndAlso p1 >= 0 AndAlso p2 > p1 Then
                json = json.Substring(p1 + 1, p2 - p1 - 1)
            End If

            Dim o = JObject.Parse(json)
            If Not o("ret")?.ToString().Contains("SUCCESS") Then Return

            Dim results = o("data")?("data")?("result")
            If results Is Nothing OrElse Not results.HasValues Then Return

            For Each item As JObject In results
                Dim inv = item("invoiceModel")
                If inv Is Nothing Then Continue For

                ' 订单号匹配（outBizId / requestNo 都是本单号）
                Dim outBizId = If(inv("outBizId")?.ToString(), "")
                Dim requestNo = If(inv("requestNo")?.ToString(), "")
                If _b2bCurrentOrderId <> "" AndAlso
               outBizId <> _b2bCurrentOrderId AndAlso requestNo <> _b2bCurrentOrderId Then
                    Continue For
                End If

                Dim p = inv("purchaserInvoiceTitleModel")
                If p Is Nothing Then Continue For
                Dim sumPayRaw = inv("amount")?.ToString()
                Dim sumPayVal As Double = 0
                Double.TryParse(sumPayRaw, sumPayVal)
                _b2bSumPayment = (sumPayVal / 100D).ToString("F2")
                _b2bInvoiceTitle = If(p("title")?.ToString(), "")
                _b2bTaxNo = If(p("taxpayerIdentify")?.ToString(), "")
                _b2bRegAddress = If(p("registerAddress")?.ToString(), "")
                _b2bBankName = If(p("bankName")?.ToString(), "")
                _b2bBankAccount = If(p("bankAccountId")?.ToString(), "")
                _b2bRegPhone = If(p("registerPhone")?.ToString(), "")

                UpdateProjectNameX()
                _invoiceTcs?.TrySetResult(True)
                Return
            Next
        Catch ex As Exception
            lblStatus.Text = "发票解析异常: " & ex.Message
        End Try
    End Sub

    ' 初始化浏览器
    Private Async Function InitializeBrowsers() As Task
        ' 初始化税务网站浏览器
        WebViewTax = New WebView2 With {
            .Dock = DockStyle.Fill
        }
        PanelTaxBrowser.Controls.Add(WebViewTax)
        Await WebViewTax.EnsureCoreWebView2Async(env)

        ' 初始化淘宝网站浏览器
        WebViewTaoBao = New WebView2 With {
            .Dock = DockStyle.Fill
        }
        PanelTaoBaoBrowser.Controls.Add(WebViewTaoBao)
        Await WebViewTaoBao.EnsureCoreWebView2Async(env)
    End Function
    ''' <summary>按淘宝格式刷新projectNamex显示（线程安全）</summary>
    Private Sub UpdateProjectNameX()
        If InvokeRequired Then Invoke(Sub() UpdateProjectNameX()) : Return
        projectNamex.Text = $"发票金额:￥{_b2bSumPayment}{vbCrLf}" &
                        $"发票抬头:{_b2bInvoiceTitle}{vbCrLf}" &
                        $"购方税号:{_b2bTaxNo}{vbCrLf}" &
                        $"开户银行:{_b2bBankName}{vbCrLf}" &
                        $"开户账号:{_b2bBankAccount}{vbCrLf}" &
                        $"企业电话:{_b2bRegPhone}{vbCrLf}" &
                        $"企业地址:{_b2bRegAddress}"
    End Sub


    ' 加载网页
    ''' <summary>只加载税务网站，程序启动时调用一次即可</summary>
    Private Sub LoadTaxWebPage()
        If taxPageLoaded Then Return
        WebViewTax.Source = New Uri(TaxSiteHelper.BuildTaxUrl(config.Region))
        taxPageLoaded = True
    End Sub
    Private Async Function ClickLoginButton() As Task(Of Boolean)
        ' 最多等 5 秒，每 300ms 查一次 .loginBtn 是否出现
        For i = 0 To 15
            Dim found As String = Await WebViewTax.CoreWebView2.ExecuteScriptAsync(
            "(function(){return document.querySelector('.loginBtn')?'yes':'no';})();")
            If found IsNot Nothing AndAlso found.Contains("yes") Then Exit For
            Await Task.Delay(300)
        Next

        Dim js As String = "(function(){var b=document.querySelector('.loginBtn');if(!b)return 'not found';b.click();return 'clicked';})();"
        Dim r As String = Await WebViewTax.CoreWebView2.ExecuteScriptAsync(js)
        Return r IsNot Nothing AndAlso r.Contains("clicked")
    End Function


    ''' <summary>只加载淘宝/1688那一侧的页面，切换模式时调用</summary>
    Private Sub LoadTaoBaoWebPage()
        lblStatus.Text = "正在加载网页..."
        If isB2BMode Then
            WebViewTaoBao.Source = New Uri("https://work.1688.com/?spm=a2638g.u_0_1001.dashboard.12.665e1768NWtnvz&_path_=sellerPro/lvyue/sellerInvoiceManage")
        Else
            WebViewTaoBao.Source = New Uri("https://qn.taobao.com/home.htm/merchant-invoice/#/invoice/agreed")
        End If
    End Sub
    ' 创建通用的Cookie恢复函数
    Private Async Function RestoreLoginState(webView As WebView2, cookiesJson As String, siteName As String) As Task
        If String.IsNullOrEmpty(cookiesJson) Then
            Return
        End If

        Try
            lblStatus.Text = $"正在恢复{siteName}网站登录状态..."

            ' 反序列化cookies
            Dim cookieList = JsonConvert.DeserializeObject(Of List(Of CookieInfo))(cookiesJson)

            If cookieList Is Nothing OrElse cookieList.Count = 0 Then
                Return
            End If

            ' 等待WebView2初始化完成
            Await webView.EnsureCoreWebView2Async(env)

            ' 设置cookies
            For Each cookieInfo In cookieList
                Try
                    Dim expires As DateTime = DateTime.MinValue
                    If Not String.IsNullOrEmpty(cookieInfo.Expires) Then
                        DateTime.TryParse(cookieInfo.Expires, expires)
                    End If

                    Dim sameSite As CoreWebView2CookieSameSiteKind
                    Select Case cookieInfo.SameSite
                        Case "Lax"
                            sameSite = CoreWebView2CookieSameSiteKind.Lax
                        Case "Strict"
                            sameSite = CoreWebView2CookieSameSiteKind.Strict
                        Case Else
                            sameSite = CoreWebView2CookieSameSiteKind.None
                    End Select

                    ' 创建并添加cookie
                    Dim cookie = webView.CoreWebView2.CookieManager.CreateCookie(
                cookieInfo.Name,
                cookieInfo.Value,
                cookieInfo.Domain,
                cookieInfo.Path)

                    cookie.IsHttpOnly = cookieInfo.HttpOnly
                    cookie.IsSecure = cookieInfo.Secure
                    cookie.SameSite = sameSite

                    ' 如果有有效的过期时间，则设置
                    If expires > DateTime.MinValue AndAlso expires < DateTime.MaxValue Then
                        cookie.Expires = expires
                    End If

                    webView.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie)
                    SetLogoutButtonVisible(True)
                Catch ex As Exception
                    Debug.WriteLine($"恢复cookie失败 {cookieInfo.Name}: {ex.Message}")
                End Try
            Next

            lblStatus.Text = $"{siteName}网站登录状态已恢复"

        Catch ex As Exception
            Debug.WriteLine($"恢复{siteName}登录状态失败: {ex.Message}")
        End Try
    End Function
    '========================================================================================
    ' ========== 隐藏 WebView2 自动下载与重命名（Button17_Click-1）==========
    Private Async Sub Button17_Click(sender As Object, e As EventArgs) Handles Button17.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        Await Xiaz1()
    End Sub

    Private Async Function Xiaz1() As Task
        Try
            ' 第一步：点击二维码交付按钮
            Dim clickScript = "(function() {
            var btns = document.querySelectorAll('button');
            for (var i = 0; i < btns.length; i++) {
                if (btns[i].textContent.trim() === '二维码交付') {
                    btns[i].click(); return true;
                }
            }
            return false;
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(clickScript)

            ' 第二步：等弹窗渲染，重试获取 URL
            Dim url As String = ""
            For i As Integer = 0 To 27
                Await Task.Delay(100)
                Dim getUrlScript = "
        (function() {
            function findInFrames(doc) {
                try {
                    var el = doc.querySelector('.qrcode-box__codeImg');
                    if (el && el.getAttribute('title')) return el.getAttribute('title');
                    for (var i = 0; i < doc.frames.length; i++) {
                        var url = findInFrames(doc.frames[i].document);
                        if (url) return url;
                    }
                } catch(e) {}
                return '';
            }
            return findInFrames(document);
        })();
    "
                Dim resultJson = Await WebViewTax.CoreWebView2.ExecuteScriptAsync(getUrlScript)
                url = resultJson.Trim().Trim(""""c)
                If Not String.IsNullOrEmpty(url) AndAlso url <> "null" Then Exit For
            Next

            ' 第三步：关闭弹窗
            Dim closeScript = "(function() {
            var dialogs = document.querySelectorAll('.t-dialog');
            for (var i = 0; i < dialogs.length; i++) {
                var header = dialogs[i].querySelector('.t-dialog__header-content');
                if (header && header.textContent.trim() === '二维码交付') {
                    var el = dialogs[i];
                    while (el && !el.__vue__) el = el.parentElement;
                    var vue = el.__vue__.$parent.$parent;
                    // 方案1: visible1
                    if (vue.$data && 'visible1' in vue.$data) {
                        vue.$data.visible1 = false;
                        vue.$forceUpdate();
                    }
                    // 方案2: dialogVisible
                    else if (vue.$data && 'dialogVisible' in vue.$data) {
                        vue.$set(vue.$data, 'dialogVisible', false);
                    }
                    break;
                }
            }
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(closeScript)

            If String.IsNullOrEmpty(url) OrElse url = "null" Then
                Me.Invoke(Sub() lblStatus.Text = "未获取到下载地址")
                Return
            End If

            ' 第四步：默认浏览器打开
            Process.Start(url)

            ' 第五步：交给隐藏的 webViewx 去下载
            Await ProcessDownloadAsync(url)

        Catch ex As Exception
            Me.Invoke(Sub() lblStatus.Text = "执行脚本失败：" & ex.Message)
        End Try
    End Function

    Private Async Function Xiaz() As Task(Of String)
        Dim url As String = ""
        Try
            ' 第一步：点击二维码交付按钮
            Dim clickScript = "(function() {
            var btns = document.querySelectorAll('button');
            for (var i = 0; i < btns.length; i++) {
                if (btns[i].textContent.trim() === '二维码交付') {
                    btns[i].click(); return true;
                }
            }
            return false;
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(clickScript)

            ' 第二步：等弹窗渲染，重试获取 URL
            For i As Integer = 0 To 27
                Await Task.Delay(100)
                Dim getUrlScript = "
        (function() {
            function findInFrames(doc) {
                try {
                    var el = doc.querySelector('.qrcode-box__codeImg');
                    if (el && el.getAttribute('title')) return el.getAttribute('title');
                    for (var i = 0; i < doc.frames.length; i++) {
                        var url = findInFrames(doc.frames[i].document);
                        if (url) return url;
                    }
                } catch(e) {}
                return '';
            }
            return findInFrames(document);
        })();
    "
                Dim resultJson = Await WebViewTax.CoreWebView2.ExecuteScriptAsync(getUrlScript)
                url = resultJson.Trim().Trim(""""c)
                If Not String.IsNullOrEmpty(url) AndAlso url <> "null" Then Exit For
            Next

            ' 第三步：关闭弹窗
            Dim closeScript = "(function() {
            var dialogs = document.querySelectorAll('.t-dialog');
            for (var i = 0; i < dialogs.length; i++) {
                var header = dialogs[i].querySelector('.t-dialog__header-content');
                if (header && header.textContent.trim() === '二维码交付') {
                    var el = dialogs[i];
                    while (el && !el.__vue__) el = el.parentElement;
                    var vue = el.__vue__.$parent.$parent;
                    // 方案1: visible1
                    if (vue.$data && 'visible1' in vue.$data) {
                        vue.$data.visible1 = false;
                        vue.$forceUpdate();
                    }
                    // 方案2: dialogVisible
                    else if (vue.$data && 'dialogVisible' in vue.$data) {
                        vue.$set(vue.$data, 'dialogVisible', false);
                    }
                    break;
                }
            }
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(closeScript)

            If String.IsNullOrEmpty(url) OrElse url = "null" Then
                Me.Invoke(Sub() lblStatus.Text = "未获取到下载地址")
                Return ""
            End If

        Catch ex As Exception
            Me.Invoke(Sub() lblStatus.Text = "执行脚本失败：" & ex.Message)
            Return ""
        End Try

        Return url
    End Function
    Private Async Function InitWebView2Async() As Task
        Try
            If webViewx IsNot Nothing Then Return

            webViewx = New WebView2 With {
                .Visible = False,
                .Dock = DockStyle.Fill
            }
            Me.Controls.Add(webViewx)

            Dim userDataFolder = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp_WebView2")
            Dim env = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
            Await webViewx.EnsureCoreWebView2Async(env)

            AddHandler webViewx.CoreWebView2.DownloadStarting, AddressOf OnDownloadStarting
            AddHandler webViewx.CoreWebView2.NavigationCompleted, AddressOf OnNavigationCompleted
        Catch ex As Exception
            MessageBox.Show("WebView2 初始化失败：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    Private Async Function ProcessDownloadAsync(url As String) As Task
        If webViewx Is Nothing Then Await InitWebView2Async()
        If webViewx Is Nothing OrElse webViewx.CoreWebView2 Is Nothing Then
            downloadTcs?.TrySetResult(Nothing)
            Return
        End If
        If isProcessingNavigation Then Return

        buyerName = ""
        downloadTcs = New TaskCompletionSource(Of String)
        webViewx.CoreWebView2.Navigate(url)
        ' 不在这里等待下载完成，函数立刻返回，保证并行
    End Function
    Private isProcessingNavigation As Boolean = False
    Private Async Sub OnNavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If isProcessingNavigation Then Return
        isProcessingNavigation = True
        Try
            If Not e.IsSuccess Then Return

            ' 提取购买方名称
            For i As Integer = 0 To 2
                Await Task.Delay(100)
                Dim script = "
                (function() {
                    let items = document.querySelectorAll('.form-item');
                    for (let i = 0; i < items.length; i++) {
                        let lab = items[i].querySelector('.item-lab');
                        if (lab && lab.textContent.trim() === '购买方名称：') {
                            let dataSpan = items[i].querySelector('.item-data');
                            if (dataSpan) return dataSpan.textContent.trim();
                        }
                    }
                    return '';
                })();
            "
                Dim resultJson = Await webViewx.CoreWebView2.ExecuteScriptAsync(script)
                Dim name = resultJson.Trim().Trim(""""c)
                If Not String.IsNullOrEmpty(name) AndAlso name <> "--" Then
                    buyerName = name
                    Exit For
                End If
            Next
            ' 点击下载按钮
            Dim clickScript = "
            (function() {
                let btns = document.querySelectorAll('button');
                for(let btn of btns) {
                    if(btn.innerText.includes('PDF下载')) {
                        btn.click();
                        return true;
                    }
                }
                return false;
            })();
        "
            Dim result = Await webViewx.CoreWebView2.ExecuteScriptAsync(clickScript)
            If result.Contains("false") Then
                Me.Invoke(Sub() lblStatus.Text = "未找到下载文件")
            End If
        Finally
            isProcessingNavigation = False
        End Try
    End Sub

    Private Sub OnDownloadStarting(sender As Object, e As CoreWebView2DownloadStartingEventArgs)
        Try
            Dim myPath As String = TextBox2.Text.Trim()
            If String.IsNullOrEmpty(myPath) Then
                myPath = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            End If
            If Not IO.Directory.Exists(myPath) Then IO.Directory.CreateDirectory(myPath)

            Dim originalFileName = IO.Path.GetFileName(e.DownloadOperation.ResultFilePath)
            Dim finalFileName = ReplacePdfName(originalFileName, buyerName)
            Dim finalPath = IO.Path.Combine(myPath, finalFileName)
            e.ResultFilePath = finalPath
            e.Handled = True

            Dim op = e.DownloadOperation
            AddHandler op.StateChanged, Sub(s2, e2)
                                            Select Case op.State
                                                Case CoreWebView2DownloadState.Completed
                                                    Me.Invoke(Sub() lblStatus.Text = "下载完成")
                                                    downloadTcs?.TrySetResult(finalPath)
                                                Case CoreWebView2DownloadState.Interrupted
                                                    Me.Invoke(Sub() lblStatus.Text = "下载失败或中断：" & op.InterruptReason.ToString())
                                                    downloadTcs?.TrySetResult(Nothing)
                                            End Select
                                        End Sub
        Catch ex As Exception
            Me.Invoke(Sub() lblStatus.Text = "下载出错")
            downloadTcs?.TrySetResult(Nothing)
        End Try
    End Sub
    Private Sub Log(msg As String)
        Try
            Dim logPath = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "debug_log.txt")
            IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}")
        Catch
        End Try
    End Sub
    Private Function ReplacePdfName(fileName As String, buyer As String) As String
        Dim parts = fileName.Split("_"c)
        If parts.Length >= 4 AndAlso Not String.IsNullOrEmpty(buyer) AndAlso buyer <> "--" Then
            parts(2) = buyer.Replace("_", "-")
            Return String.Join("_", parts)
        End If
        Return fileName
    End Function
    ' ========== 隐藏 WebView2 自动下载与重命名（Button17_Click）==========↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑

    ' 设置事件处理器
    Private Sub SetupEventHandlers()

        ' 税务网站事件
        AddHandler WebViewTax.NavigationStarting, AddressOf WebViewTax_NavigationStarting
        AddHandler WebViewTax.NavigationCompleted, AddressOf WebViewTax_NavigationCompleted

        ' 淘宝网站事件
        AddHandler WebViewTaoBao.NavigationStarting, AddressOf WebViewTaoBao_NavigationStarting
        AddHandler WebViewTaoBao.NavigationCompleted, AddressOf WebViewTaoBao_NavigationCompleted
    End Sub

    ' 税务网站导航开始
    Private Sub WebViewTax_NavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        Me.Invoke(Sub()
                      lblStatus.Text = "正在加载税务网站..."
                  End Sub)
    End Sub

    ' 税务网站导航完成
    Private Sub WebViewTax_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If e.IsSuccess Then
            Me.Invoke(Sub()
                          lblStatus.Text = "税务网站加载完成"
                      End Sub)
            Cookies()
        Else
            Me.Invoke(Sub()
                          lblStatus.Text = "税务网站加载失败"
                      End Sub)
        End If
    End Sub

    ' 淘宝网站导航开始
    Private Sub WebViewTaoBao_NavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        Me.Invoke(Sub()
                      lblStatus.Text = "正在加载网站..."
                  End Sub)
    End Sub

    ' 淘宝网站导航完成
    Private Async Sub WebViewTaoBao_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If e.IsSuccess Then
            Me.Invoke(Sub()
                          lblStatus.Text = "网站加载完成"
                      End Sub)
            Cookies()
            ' ===== 新增：每次导航完成后重新捕获登录iframe =====
            If Not isFrameHandlerAdded Then
                isFrameHandlerAdded = True
                AddHandler WebViewTaoBao.CoreWebView2.FrameCreated, Sub(s, fe)
                                                                        ' 直接全部记录，不等 NavigationStarting
                                                                        taobaoLoginFrame = fe.Frame
                                                                        ' 同时也订阅 NavigationStarting 作为备用
                                                                        AddHandler fe.Frame.NavigationStarting, Sub(fs, fne)
                                                                                                                    If fne.Uri.Contains("havanalogin.taobao.com") Then
                                                                                                                        taobaoLoginFrame = fe.Frame
                                                                                                                    End If
                                                                                                                End Sub
                                                                    End Sub
            End If
            ' 只执行一次
            If Not isScriptExecuted Then
                isScriptExecuted = True
                ExecuteMonitoringScript()
            End If
            ' 识别天猫还是淘宝
            If Not e.IsSuccess Then Return

            Await DetectSite()

        Else
            Me.Invoke(Sub()
                          lblStatus.Text = "网站加载失败"
                      End Sub)
        End If
    End Sub
    Private Async Sub ExecuteMonitoringScript()
        Try
            Dim jsCode As String = "
(function() {
    if (window.taobaoMonitorInjected) return;
    window.taobaoMonitorInjected = true;
    
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const url = args[0];
        if (url.includes('mtop.taobao.sell.pc.manage') && args[1] && args[1].body) {
            const match = args[1].body.match(/data=([^&]+)/);
            if (match) {
                try {
                    const dataStr = decodeURIComponent(match[1]);
                    window.capturedTaobaoData = JSON.parse(dataStr);
                    console.log('✅ 捕获成功:', window.capturedTaobaoData);
                } catch(e) {}
            }
        }
        return originalFetch.apply(this, args);
    };
    
    console.log('✅ 监听已启动');
})();
"

            Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(jsCode)

            Me.Invoke(Sub()
                          lblStatus.Text = "已就绪"
                      End Sub)

        Catch ex As Exception
            Me.Invoke(Sub()
                          lblStatus.Text = "脚本执行失败: " & ex.Message
                      End Sub)
        End Try
        WebViewTaoBao.Reload()
    End Sub

    ' 获取捕获的数据
    Private Async Function GetCapturedData() As Task(Of String)
        Try
            Return Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.capturedTaobaoData || null)")
        Catch ex As Exception
            Return Nothing
        End Try
    End Function
    Private Async Sub HideWebViewScrollbar(webView As WebView2)
        ' 确保 CoreWebView2 已初始化
        Await webView.EnsureCoreWebView2Async(Nothing)

        ' 页面每次导航完成后都隐藏滚动条（包括首次加载）
        AddHandler webView.CoreWebView2.NavigationCompleted, Async Sub(s, e)
                                                                 Dim script As String = "
            var style = document.createElement('style');
            style.textContent = `
                ::-webkit-scrollbar { display: none; }
                body, html { overflow: auto; -ms-overflow-style: none; scrollbar-width: none; }
            `;
            document.head.appendChild(style);
        "
                                                                 Await webView.CoreWebView2.ExecuteScriptAsync(script)
                                                             End Sub
    End Sub
    ' 注入自适应
    Private Async Function SetAutoZoom(webView As WebView2) As Task
        Try
            ' 如果控件为空，直接返回
            If webView Is Nothing Then Return
            If webView.CoreWebView2 Is Nothing Then
                Await webView.EnsureCoreWebView2Async(Nothing)
            End If
            ' 再次检查 CoreWebView2 是否初始化成功
            If webView.CoreWebView2 Is Nothing Then Return
            ' 获取 WebView2 实际显示区域大小
            Dim clientWidth As Double = webView.ClientSize.Width
            Dim clientHeight As Double = webView.ClientSize.Height

            If clientWidth <= 0 OrElse clientHeight <= 0 Then Return

            ' 设计基准尺寸：使得最大化时缩放 = 0.8（即 1588 / designWidth = 0.8）1476 1371

            Dim designWidth As Double = 1476
            Dim designHeight As Double = 1371

            ' 计算缩放比例（取较小值保证内容完整显示，且最大为 1 不放大）
            Dim widthRatio As Double = clientWidth / designWidth
            Dim heightRatio As Double = clientHeight / designHeight
            Dim zoom As Double = Math.Min(widthRatio, heightRatio)

            ' 限制缩放范围：最小 0.5，最大 1（不放大）
            zoom = Math.Max(0.5, Math.Min(1.0, zoom))
            'MsgBox(clientWidth.ToString() & "       " & clientHeight.ToString())
            webView.ZoomFactor = zoom
        Catch ex As Exception
            Debug.WriteLine($"SetAutoZoom 异常: {ex.Message}")
        End Try
    End Function

    Private Async Sub Form1_Resize(sender As Object, e As EventArgs) Handles MyBase.Resize

        Await SetAutoZoom(WebViewTax)


        Await SetAutoZoom(WebViewTaoBao)

    End Sub

    ' 保存登录信息菜单项点击
    Private Sub 保存登录信息(sender As Object, e As EventArgs) Handles Button5.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        Dim loginForm As New LoginInfoForm(config)
        If loginForm.ShowDialog() = DialogResult.OK Then
            ' 保存配置
            SaveConfig()
            lblStatus.Text = "登录信息已保存"
        End If
    End Sub



    ' 填充登录信息菜单项点击
    Private Async Sub 填充登录信息(sender As Object, e As EventArgs) Handles Button6.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        ' 根据当前选中的账号索引，获取对应的税号、手机、密码
        Dim taxNumber As String
        Dim phone As String
        Dim password As String

        Select Case config.CurrentTaxAccountIndex
            Case 0
                taxNumber = config.TaxNumber1
                phone = config.Phone1
                password = config.Passwordx1
            Case 1
                taxNumber = config.TaxNumber2
                phone = config.Phone2
                password = config.Passwordx2
            Case 2
                taxNumber = config.TaxNumber3
                phone = config.Phone3
                password = config.Passwordx3
            Case 3
                taxNumber = config.TaxNumber4
                phone = config.Phone4
                password = config.Passwordx4
            Case 4
                taxNumber = config.TaxNumber5
                phone = config.Phone5
                password = config.Passwordx5
            Case Else
                ' 如果索引无效，可清空或抛出异常
                taxNumber = ""
                phone = ""
                password = ""
        End Select

        ' 验证数据完整性
        If String.IsNullOrEmpty(taxNumber) OrElse
       String.IsNullOrEmpty(phone) OrElse
       String.IsNullOrEmpty(password) Then
            lblStatus.Text = "请先保存登录信息（当前选中的账号）！"
            Return
        End If

        lblStatus.Text = "正在自动识别并填充登录信息..."

        '手动转义单引号
        Dim safeTaxNum As String = taxNumber.Replace("'", "\'")
        Dim safePhone As String = phone.Replace("'", "\'")
        Dim safePwd As String = password.Replace("'", "\'")

        ' 构建JavaScript脚本
        Dim script As String = "// 智能填充登录表单
        function smartFillLoginForm() {
            var taxNum = '" & safeTaxNum & "';
            var phone = '" & safePhone & "';
            var pwd = '" & safePwd & "';

            var results = [];
            var successCount = 0;

            // 尝试多种选择器填充
            var selectors = [
                {type: '税号', selectors: ['.creditCode input', 'input[placeholder*=""统一社会信用代码""]', 'input[placeholder*=""纳税人识别号""]']},
                {type: '手机', selectors: ['.mobileInp input', 'input[placeholder*=""手机号码""]', 'input[placeholder*=""居民身份证号码""]']},
                {type: '密码', selectors: ['.pwdInp input', 'input[type=""password""]', 'input[placeholder*=""个人用户密码""]']}
            ];

            var values = [taxNum, phone, pwd];

            for (var i = 0; i < selectors.length; i++) {
                var filled = false;
                var type = selectors[i].type;
                var selectorList = selectors[i].selectors;
                var value = values[i];

                for (var j = 0; j < selectorList.length; j++) {
                    var element = document.querySelector(selectorList[j]);
                    if (element) {
                        element.value = value;
                        element.dispatchEvent(new Event('input', { bubbles: true }));
                        element.dispatchEvent(new Event('change', { bubbles: true }));
                        element.dispatchEvent(new Event('blur', { bubbles: true }));

                        results.push(type + ': 成功 (使用选择器: ' + selectorList[j] + ')');
                        successCount++;
                        filled = true;
                        break;
                    }
                }

                if (!filled) {
                    results.push(type + ': 失败 (未找到输入框)');
                }
            }

            if (successCount >= 2) {
                setTimeout(function() {
                    var loginBtn = document.querySelector('button.loginCls');
                    if (loginBtn) {
                        loginBtn.click();
                        results.push('登录按钮: 已点击');
                    } else {
                        results.push('登录按钮: 未找到');
                    }
                }, 1000);
            }

            window.chrome.webview.postMessage({
                type: 'fillResult',
                data: {
                    success: successCount,
                    total: 3,
                    results: results,
                    message: '自动填充完成: ' + successCount + '/3 成功'
                }
            });
        }

        smartFillLoginForm();"

        ' 执行JavaScript
        Await WebViewTax.ExecuteScriptAsync(script)
        lblStatus.Text = "正在自动填充登录信息..."
    End Sub

    Private Sub 保存登录信息2(sender As Object, e As EventArgs) Handles Button19.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        Dim loginForm As New TwoAccountLoginForm(config)
        If loginForm.ShowDialog() = DialogResult.OK Then
            SaveConfig()
            lblStatus.Text = "登录信息已保存"
        End If
    End Sub

    Private Async Sub 填充登录信息2(sender As Object, e As EventArgs) Handles Button20.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        If isProcessingLogin Then Return
        isProcessingLogin = True

        ' ========== 1688：登录页是顶层页面，直接对 WebViewTaoBao 执行 ==========
        If isB2BMode Then
            Await FillLoginOn1688()
            isProcessingLogin = False
            Return
        End If

        ' ========== 淘宝/天猫：原有逻辑不动 ==========
        If taobaoLoginFrame Is Nothing Then
            lblStatus.Text = "未到登录页，请跳转到登录页"
            isProcessingLogin = False
            Return
        End If

        Dim account As String = String.Empty
        Dim password As String = String.Empty
        GetLoginAccount(account, password)

        If String.IsNullOrEmpty(account) OrElse String.IsNullOrEmpty(password) Then
            lblStatus.Text = "请先设置账号密码"
            isProcessingLogin = False
            Return
        End If

        Dim safeAccount = account.Replace("\", "\\").Replace("'", "\'")
        Dim safePassword = password.Replace("\", "\\").Replace("'", "\'")

        Dim js = "
(function() {
    var userEl = document.getElementById('fm-login-id');
    var pwdEl  = document.getElementById('fm-login-password');
    if (!userEl || !pwdEl) return 'fail:' + (!!userEl) + '/' + (!!pwdEl);
    userEl.value = '" & safeAccount & "';
    userEl.dispatchEvent(new Event('input',  { bubbles:true }));
    userEl.dispatchEvent(new Event('change', { bubbles:true }));
    pwdEl.value = '" & safePassword & "';
    pwdEl.dispatchEvent(new Event('input',  { bubbles:true }));
    pwdEl.dispatchEvent(new Event('change', { bubbles:true }));
    var btn = document.querySelector('.fm-button.fm-submit.password-login')
           || document.querySelector('button[type=""submit""]');
    if (btn) { btn.disabled = false; btn.click(); return 'ok'; }
    return 'fail:no_btn';
})();"

        Dim result = (Await taobaoLoginFrame.ExecuteScriptAsync(js)).Trim(""""c)
        If result = "ok" Then
            lblStatus.Text = $"已提交登录 {account}（如有滑块请手动拖动）"
            SetLogoutButtonVisible(True)
            Button23.Enabled = Not Visible
        Else
            lblStatus.Text = "登录填充失败：" & result
        End If
        isProcessingLogin = False
    End Sub
    ''' <summary>按当前账号索引取登录账号密码</summary>
    Private Sub GetLoginAccount(ByRef account As String, ByRef password As String)
        account = ""
        password = ""
        Select Case config.CurrentAccountIndex
            Case 0 : account = config.Account1 : password = config.Password1
            Case 1 : account = config.Account2 : password = config.Password2
            Case 2 : account = config.Account3 : password = config.Password3
            Case 3 : account = config.Account4 : password = config.Password4
            Case 4 : account = config.Account5 : password = config.Password5
        End Select
    End Sub
    ''' <summary>1688 登录页填充（顶层页面，无需 iframe）</summary>
    Private Async Function FillLoginOn1688() As Task
        ' 先确认在登录页
        Dim pageCheck = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(
        "document.getElementById('fm-login-id') !== null ? 'yes' : 'no'")
        If pageCheck.Trim(""""c) <> "yes" Then
            lblStatus.Text = "未到1688登录页，请先进入登录页"
            Return
        End If

        Dim account As String = ""
        Dim password As String = ""
        GetLoginAccount(account, password)

        If String.IsNullOrEmpty(account) OrElse String.IsNullOrEmpty(password) Then
            lblStatus.Text = "请先设置账号密码"
            Return
        End If

        Dim safeAccount = account.Replace("\", "\\").Replace("'", "\'")
        Dim safePassword = password.Replace("\", "\\").Replace("'", "\'")


        ' ===== 只负责填充和点登录 =====
        Dim jsFill = "
(function() {
    var userEl = document.getElementById('fm-login-id');
    var pwdEl  = document.getElementById('fm-login-password');
    if (!userEl || !pwdEl) return 'fail:nofield';
    userEl.value = '" & safeAccount & "';
    userEl.dispatchEvent(new Event('input',  { bubbles:true }));
    userEl.dispatchEvent(new Event('change', { bubbles:true }));
    pwdEl.value = '" & safePassword & "';
    pwdEl.dispatchEvent(new Event('input',  { bubbles:true }));
    pwdEl.dispatchEvent(new Event('change', { bubbles:true }));
    var btn = document.querySelector('.fm-button.fm-submit.password-login')
           || document.querySelector('button[type=""submit""]');
    if (btn) { btn.disabled = false; btn.click(); return 'ok'; }
    return 'fail:no_btn';
})();"

        Dim result = (Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(jsFill)).Trim(""""c)
        If result = "ok" Then
            lblStatus.Text = $"已提交登录 {account}（如有滑块请手动拖动）"
            SetLogoutButtonVisible(True)
        Else
            lblStatus.Text = "登录填充失败：" & result
        End If
    End Function
    Private Sub SetLogoutButtonVisible(visible As Boolean)
        Button21.Visible = visible
        Button23.Enabled = Not visible   ' Button21在 → Button23禁用；Button21消失 → Button23可点
    End Sub

    Private Async Sub 退出登录(sender As Object, e As EventArgs) Handles Button21.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        If WebViewTaoBao Is Nothing OrElse WebViewTaoBao.CoreWebView2 Is Nothing Then
            lblStatus.Text = "浏览器未初始化"
            Return
        End If

        ' ========== 1688：点击"登出"链接 ==========
        If isB2BMode Then
            Try
                Dim js = "
    (function(){
        var all = document.querySelectorAll('a');
        for(var i=0;i<all.length;i++){
            var link = all[i];
            if(link.textContent.trim() === '登出' ||
               (link.href && link.href.indexOf('signout.htm') > -1)){
                link.click();
                return 'clicked';
            }
        }
        return 'not_found';
    })();"

                Dim result = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(js)
                Dim resStr = result.Trim(""""c)
                If resStr = "clicked" Then
                    lblStatus.Text = "已退出1688登录，正在跳转发票管理页..."
                    SetLogoutButtonVisible(False)
                    Await Task.Delay(2000)
                    WebViewTaoBao.CoreWebView2.Navigate(
                    "https://work.1688.com/?_path_=sellerPro/lvyue/sellerInvoiceManage")
                Else
                    lblStatus.Text = $"未找到登出链接: {resStr}"
                End If
            Catch ex As Exception
                lblStatus.Text = $"1688退出出错: {ex.Message}"
            End Try
            Return
        End If

        ' ========== 淘宝/天猫：点击"退出当前账号" ==========
        Dim jsTb = "
    (function(){
        var all = document.querySelectorAll('*');
        for(var i=0;i<all.length;i++){
            if(all[i].textContent.trim() === '退出当前账号'){
                all[i].click();
                return 'clicked_by_text';
            }
        }
        return 'not_found';
    })();"

        Try
            Dim result2 = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(jsTb)
            Dim resStr2 = result2.Trim(""""c)
            If resStr2.StartsWith("clicked") Then
                lblStatus.Text = "已退出登录"
                SetLogoutButtonVisible(False)
            Else
                lblStatus.Text = $"未找到退出按钮: {resStr2}"
            End If
        Catch ex As Exception
            lblStatus.Text = $"退出出错: {ex.Message}"
        End Try
    End Sub




    ' 刷新税务网站菜单项点击
    Private Sub 刷新税务网站(sender As Object, e As EventArgs) Handles Button4.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        WebViewTax.Reload()
        lblStatus.Text = "刷新税务网站..."
    End Sub

    ' 显示开票信息
    Private Function DisplayInvoiceInfo() As String
        Dim sb As New StringBuilder()

        Dim fields As String() = {"发票抬头", "购方税号", "开户银行", "开户账号", "企业电话", "企业地址", "邮箱"}
        For Each field In fields
            Dim value As String = ""
            If invoiceData.ContainsKey(field) Then
                value = invoiceData(field)
            End If
            ' 邮箱为空时跳过不显示
            If field = "邮箱" AndAlso String.IsNullOrWhiteSpace(value) Then
                Continue For
            End If
            sb.AppendLine(field & "：" & value)
        Next

        ' AI 额外识别的字段也显示
        For Each kv In invoiceData
            If Not fields.Contains(kv.Key) Then
                sb.AppendLine(kv.Key & "：" & kv.Value)
            End If
        Next

        projectNamex.Text = sb.ToString()
        Return sb.ToString()
    End Function

    Private Const API_URL As String = "https://open.bigmodel.cn/api/paas/v4/chat/completions"
    Private Async Sub BtnManualSave_Click(sender As Object, e As EventArgs) Handles btnManualSave.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Dim inputText = txtManualInput.Text.Trim()

        If String.IsNullOrEmpty(inputText) Then
            lblStatus.Text = "请输入开票信息！"
            Return
        End If

        lblStatus.Text = "智能解析中，请稍候..."
        btnManualSave.Enabled = False
        invoiceData.Clear()

        Try
            Await SmartParseInvoiceInfoAsync(inputText)

            ' 确保 invoiceData 已填充完毕再继续
            If invoiceData.Count = 0 Then
                lblStatus.Text = "未能解析出有效的开票信息，请确保格式正确。"
                Return
            End If

            _result = DisplayInvoiceInfo()

            Dim companyName As String = ExtractValue(_result, "发票抬头")
            Dim taxNumber As String = ExtractValue(_result, "购方税号")

            Dim script As String = "(function() {
            const dataMap = {
                'gmfmc': """ & companyName & """,
                'gmfnsrsbh': """ & taxNumber & """
            };
            function fillSingleField(key, value) {
                const container = document.querySelector('.t-form-item__' + key);
                if (!container) return;
                const input = container.querySelector('input');
                if (!input) return;
                input.removeAttribute('readonly');
                input.removeAttribute('disabled');
                input.disabled = false;
                input.readOnly = false;
                input.focus();
                input.click();
                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSetter.call(input, value);
                const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
                for (let evtType of events) {
                    const event = new Event(evtType, { bubbles: true, cancelable: true });
                    Object.defineProperty(event, 'target', {writable: false, value: input});
                    input.dispatchEvent(event);
                }
                try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            }
            Object.entries(dataMap).forEach(([key, value]) => { fillSingleField(key, value); });
        })();"

            If WebViewTax.CoreWebView2 IsNot Nothing Then
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
            End If
            Await BtnExample_Click1()
            lblStatus.Text = String.Format("智能解析到 {0} 条开票信息", invoiceData.Count)
        Catch ex As Exception
            lblStatus.Text = "解析失败：" & ex.Message
        Finally
            btnManualSave.Enabled = True
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Sub

    Private Async Function SmartParseInvoiceInfoAsync(text As String) As Task
        Dim systemPrompt As String =
    "You are an invoice information extraction assistant. " &
    "Extract all fields from the invoice text and map them to these Chinese field names: " &
    "发票抬头 (may appear as: 公司名称, 单位名称, 账户名称, 开票抬头, 抬头, or infer from context e.g. a line containing 公司/学校/集团/有限/股份/个体户/幼儿园/工作室/商行), " &
    "购方税号 (may appear as: 统一社会信用代码, 纳税人识别号, 税号, 信用代码, or infer: a string of 15-20 alphanumeric characters), " &
    "企业地址 (may appear as: 地址, 注册地址, 单位地址, or infer: a line containing 省/市/区/县/路/街/号), " &
    "企业电话 (may appear as: 电话, 联系电话, 手机, or infer: a string of 7-13 digits with possible dashes), " &
    "开户银行 (may appear as: 开户行, 银行名称, or infer: a line containing 银行/支行/信用社), " &
    "开户账号 (may appear as: 账号, 银行账号, 账户号码, or infer: a string of 10-25 digits), " &
    "邮箱 (may appear as: 电子邮箱, 电子发票请发, 发票邮箱, or infer: a string containing @). " &
    "IMPORTANT: Even if field labels are missing, infer the field type from the value format or context. " &
    "IMPORTANT: If an address spans multiple lines or contains line breaks or extra spaces, merge them into a single continuous string. " &
    "Return ONLY a single valid JSON object. " &
    "Use ONLY standard ASCII characters: double quotes, colon, curly braces. " &
    "No markdown, no arrays, no explanation."

        ' 转义特殊字符，手动拼 JSON，零第三方依赖
        Dim escapedSystem = EscapeJson(systemPrompt)
        Dim escapedText = EscapeJson(text)

        Dim json As String = String.Format(
        "{{""model"":""glm-4-flash"",""temperature"":0.1,""messages"":[" &
        "{{""role"":""system"",""content"":""{0}""}}," &
        "{{""role"":""user"",""content"":""{1}""}}]}}",
        escapedSystem, escapedText)

        Dim apiKey As String = If(config?.ZhipuApiKey, "").Trim()
        If apiKey = "" Then
            MessageBox.Show("请先在【设置】里填写智谱AI的Key！(在智谱申请免费的key)", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim request = DirectCast(WebRequest.Create(API_URL), HttpWebRequest)
        request.Method = "POST"
        request.ContentType = "application/json"
        request.Headers.Add("Authorization", "Bearer " & apiKey)
        request.Timeout = 30000

        Dim bodyBytes = Encoding.UTF8.GetBytes(json)
        Using stream = Await request.GetRequestStreamAsync()
            Await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length)
        End Using

        Dim response = DirectCast(Await request.GetResponseAsync(), HttpWebResponse)
        Dim responseStr As String
        Using reader As New IO.StreamReader(response.GetResponseStream(), Encoding.UTF8)
            responseStr = Await reader.ReadToEndAsync()
        End Using

        ' 提取 content 字段
        Dim contentMatch = Regex.Match(responseStr, """content""\s*:\s*""((?:[^""\\]|\\.)*)""")
        If Not contentMatch.Success Then
            Throw New Exception("响应解析失败：" & responseStr)
        End If


        Dim resultText = contentMatch.Groups(1).Value
        resultText = resultText.Replace("\n", "").Replace("```json", "").Replace("```", "").Trim()
        resultText = resultText.Replace("\""", """")
        resultText = resultText.Replace("\", "")  ' ← 加这行，去掉多余的反斜杠

        'Throw New Exception("AI返回内容：" & resultText)  ' ← 临时调试用

        ' 同时匹配有值和无值的字段："key":"value" 或 "key":"" 或 "key":
        Dim pattern1 = Regex.Matches(resultText, """([^""]+)""\s*:\s*(?:""([^""]*)""|(?=[,\}]))")
        For Each m As Match In pattern1
            Dim key = m.Groups(1).Value.Trim()
            Dim value = If(m.Groups(2).Success, m.Groups(2).Value.Trim(), "")
            If Not String.IsNullOrWhiteSpace(key) Then
                If key = "邮箱" AndAlso String.IsNullOrWhiteSpace(value) Then
                    Continue For
                End If
                invoiceData(key) = value
            End If
        Next
    End Function

    ' 转义 JSON 字符串中的特殊字符
    Private Function EscapeJson(input As String) As String
        Return input _
        .Replace("\", "\\") _
        .Replace("""", "\""") _
        .Replace(vbCrLf, "\n") _
        .Replace(vbCr, "\n") _
        .Replace(vbLf, "\n") _
        .Replace(vbTab, "\t")
    End Function

    ' 保存淘宝账号信息
    ' Cookie信息类
    Public Class CookieInfo
        Public Property Name As String
        Public Property Value As String
        Public Property Domain As String
        Public Property Path As String
        Public Property Expires As String
        Public Property HttpOnly As Boolean
        Public Property Secure As Boolean
        Public Property SameSite As String
    End Class

    Private Async Sub Cookies()
        Try
            Await SaveWebViewCookies(WebViewTax, "chinatax.gov.cn", "TaxCookies")
            If isB2BMode Then
                Await SaveWebViewCookies(WebViewTaoBao, "1688.com", "B2BCookies")
            Else
                Await SaveWebViewCookies(WebViewTaoBao, "taobao.com", "TaoBaoCookies")
            End If
            SaveConfig()
        Catch
        End Try
    End Sub

    ' 通用的Cookie保存函数
    Private Async Function SaveWebViewCookies(webView As WebView2, domain As String, configPropertyName As String) As Task
        Try
            ' 获取指定域名的cookies
            Dim cookies = Await webView.CoreWebView2.CookieManager.GetCookiesAsync($"https://{domain}")

            ' 将cookies序列化为JSON保存
            Dim cookieList As New List(Of CookieInfo)

            For Each cookie In cookies
                Try
                    Dim cookieInfo As New CookieInfo With {
                    .Name = cookie.Name,
                    .Value = cookie.Value,
                    .Domain = cookie.Domain,
                    .Path = cookie.Path,
                    .HttpOnly = cookie.IsHttpOnly,
                    .Secure = cookie.IsSecure,
                    .SameSite = cookie.SameSite.ToString()
                }

                    ' 处理过期时间
                    If cookie.Expires > DateTime.MinValue AndAlso cookie.Expires < DateTime.MaxValue Then
                        cookieInfo.Expires = cookie.Expires.ToString("O")
                    Else
                        cookieInfo.Expires = ""
                    End If

                    cookieList.Add(cookieInfo)
                Catch ex As Exception
                    Debug.WriteLine($"处理cookie失败 {cookie.Name}: {ex.Message}")
                End Try
            Next

            ' 使用反射设置配置属性
            Dim prop = config.GetType().GetProperty(configPropertyName)
            If prop IsNot Nothing AndAlso prop.CanWrite Then
                prop.SetValue(config, JsonConvert.SerializeObject(cookieList))
            End If

        Catch ex As Exception
            Debug.WriteLine($"保存{domain}的cookies失败: {ex.Message}")
        End Try
    End Function

    ' 清空开票信息菜单项点击
    Private Sub 清空开票信息(sender As Object, e As EventArgs) Handles Button2.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        invoiceData.Clear()
        projectNamex.Text = ""
        txtManualInput.Text = ""
        lblStatus.Text = "开票信息已清空"
    End Sub


    ' 刷新淘宝网站菜单项点击
    Private Async Sub 刷新网站(sender As Object, e As EventArgs) Handles Button3.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        WebViewTaoBao.Reload()
        lblStatus.Text = "刷新网站..."

        Await DetectSite()
    End Sub
    Private Async Function DetectSite() As Task
        Try
            If isB2BMode Then
                shangjiang = "2"
                lblStatus.Text = "当前是1688网站"
                Return
            End If

            Dim resultJson = Await WebViewTaoBao.ExecuteScriptAsync(
            "document.querySelector('link[href*=""tmall""]') !== null")

            If Not String.IsNullOrEmpty(resultJson) Then
                Dim resultStr = resultJson.Trim().Trim(""""c).ToLower()
                If resultStr = "true" Then
                    shangjiang = "0" : lblStatus.Text = "当前是天猫网站"
                Else
                    shangjiang = "1" : lblStatus.Text = "当前是淘宝网站"
                End If
            End If
        Catch ex As Exception
            MessageBox.Show($"检查失败: {ex.Message}")
        End Try
    End Function


    ' 加载配置
    Private Sub LoadConfig()
        Try
            If File.Exists(configPath) Then
                Dim json = File.ReadAllText(configPath, Encoding.UTF8)
                config = JsonConvert.DeserializeObject(Of AppConfig)(json)
            End If
        Catch ex As Exception
            ' 配置文件损坏，使用默认配置
            config = New AppConfig()
        End Try
    End Sub

    ' 保存配置
    Private Sub SaveConfig()
        Try
            Dim json = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented)
            File.WriteAllText(configPath, json, Encoding.UTF8)
        Catch ex As Exception
            lblStatus.Text = "保存配置失败："
        End Try
    End Sub
    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBox1.SelectedIndexChanged
        ' 避免在同步设置时触发事件循环
        RemoveHandler ComboBox2.SelectedIndexChanged, AddressOf ComboBox2_SelectedIndexChanged
        ComboBox2.SelectedIndex = ComboBox1.SelectedIndex
        AddHandler ComboBox2.SelectedIndexChanged, AddressOf ComboBox2_SelectedIndexChanged
        config.CurrentAccountIndex = ComboBox1.SelectedIndex
        config.CurrentTaxAccountIndex = ComboBox2.SelectedIndex
    End Sub
    Private Sub ComboBox2_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBox2.SelectedIndexChanged
        RemoveHandler ComboBox1.SelectedIndexChanged, AddressOf ComboBox1_SelectedIndexChanged
        ComboBox1.SelectedIndex = ComboBox2.SelectedIndex
        AddHandler ComboBox1.SelectedIndexChanged, AddressOf ComboBox1_SelectedIndexChanged
        config.CurrentAccountIndex = ComboBox1.SelectedIndex
        config.CurrentTaxAccountIndex = ComboBox2.SelectedIndex
    End Sub
    ' 帮助按钮点击
    Private Sub BtnHelp_Click(sender As Object, e As EventArgs) Handles btnHelp.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        Using dlg As New Form3()
            dlg.ShowDialog(Me)
        End Using
    End Sub

    ' 关于按钮点击
    Private Sub BtnAbout_Click(sender As Object, e As EventArgs) Handles btnAbout.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        Using dlg As New Form2()
            dlg.ShowDialog(Me)
        End Using
    End Sub

    ' 获取开票信息
    ' 将 Invoicing 改为有返回值的函数
    Private Async Function Invoicing() As Task(Of String)
        Try
            Dim script As String = "
        (function() {
            function getVal(id, labelText) {
                var value = '';
                
                var label = document.querySelector('label[for=""' + id + '""]');
                if (label) {
                    var row = label.closest('[role=""row""]');
                    if (row) {
                        var valueSpan = row.querySelector('span.invoiceDetail_formValue__esg\\+o');
                        if (!valueSpan) {
                            valueSpan = row.querySelector('[class*=""invoiceDetail_formValue""]');
                        }
                        if (valueSpan) {
                            value = valueSpan.textContent.trim();
                        }
                    }
                }
                
                if (!value && labelText) {
                    var labels = document.querySelectorAll('label');
                    for (var i = 0; i < labels.length; i++) {
                        if (labels[i].textContent.trim() === labelText) {
                            var row = labels[i].closest('[role=""row""]');
                            if (row) {
                                var valueSpan = row.querySelector('span.invoiceDetail_formValue__esg\\+o');
                                if (!valueSpan) {
                                    valueSpan = row.querySelector('[class*=""invoiceDetail_formValue""]');
                                }
                                if (valueSpan) {
                                    value = valueSpan.textContent.trim();
                                    break;
                                }
                            }
                        }
                    }
                }
                
                return value || '';
            }

            var result = [];
            result.push('发票金额:' + (document.querySelector('.invoiceDetail_amount__aKfbM')?.textContent.trim() || ''));
            result.push('发票抬头:' + getVal('payerName', '发票抬头'));
            result.push('购方税号:' + getVal('payerRegisterNo', '购方税号'));
            result.push('开户银行:' + getVal('payerBank', '开户银行'));
            result.push('开户账号:' + getVal('payerBankaccount', '开户账号'));
            result.push('企业电话:' + getVal('payerPhone', '企业电话'));
            result.push('企业地址:' + getVal('payerAddress', '企业地址'));
            
            return result.join('\r\n');
        })();"

            Dim jsonResult As String = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(script)

            If Not String.IsNullOrEmpty(jsonResult) AndAlso jsonResult <> "null" AndAlso jsonResult <> "undefined" Then
                Dim textResult As String = jsonResult.Trim(""""c).Replace("\r\n", vbCrLf)
                projectNamex.Text = textResult  ' 保留原有写入 TextBox 的逻辑
                Return textResult
            Else
                projectNamex.Text = "未能提取到数据"
                Return String.Empty
            End If
        Catch ex As Exception
            projectNamex.Text = $"提取失败: {ex.Message}"
            Return String.Empty
        End Try
    End Function
    ' 只填入企业地址、企业电话、开户银行、开户账号
    Private Async Sub BtnExample_Click(sender As Object, e As EventArgs) Handles btnExample.Click
        Await Butt_yz()
        Await BtnExample_Click1()
    End Sub
    Private Async Function BtnExample_Click1() As Task

        Try
            ' 直接用 FillCompanyNameAndTax 已缓存的数据
            Dim address As String = ExtractValue(projectNamex.Text, "企业地址")
            Dim phone As String = ExtractValue(projectNamex.Text, "企业电话")
            Dim bank As String = ExtractValue(projectNamex.Text, "开户银行")
            Dim bankAccount As String = ExtractValue(projectNamex.Text, "开户账号")

            Dim script As String = "(function() {
            const dataMap = {
                'dz': '" & address & "',
                'lxdh': '" & phone & "',
                'yhyywdmc': '" & bank & "',
                'yhzh': '" & bankAccount & "'
            };
            function fillSingleField(key, value) {
                const container = document.querySelector('.t-form-item__' + key);
                if (!container) return;
                const input = container.querySelector('input');
                if (!input) return;
                input.removeAttribute('readonly');
                input.removeAttribute('disabled');
                input.disabled = false;
                input.readOnly = false;
                input.focus();
                input.click();
                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSetter.call(input, value);
                const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
                for (let evtType of events) {
                    const event = new Event(evtType, { bubbles: true, cancelable: true });
                    Object.defineProperty(event, 'target', {writable: false, value: input});
                    input.dispatchEvent(event);
                }
                try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            }
            Object.entries(dataMap).forEach(([key, value]) => { fillSingleField(key, value); });
        })();"
            If WebViewTax.CoreWebView2 IsNot Nothing Then
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            ' 错误记录
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Function

    Private Async Sub SimpleFillButton_Click(sender As Object, e As EventArgs) Handles Button7.Click '验证税号
        Await Butt_yz()
        Await FillCompanyInformation()
        Me.ActiveControl = Nothing
        Me.Focus()
    End Sub
    ' 只填入企业地址、企业电话、开户银行、开户账号（直接从网页获取）
    Private Async Function FillCompanyInformation() As Task
        Try
            Dim rowCount As Integer = CInt(NumericUpDown1.Value)
            If rowCount = 0 Then
                Dim address As String
                Dim phone As String
                Dim bank As String
                Dim bankAccount As String

                If isB2BMode Then
                    ' 1688模式：数据来自 ParseB2bInvoice 解析出的字段
                    address = _b2bRegAddress
                    phone = _b2bRegPhone
                    bank = _b2bBankName
                    bankAccount = _b2bBankAccount
                Else
                    ' 淘宝/天猫模式：数据来自 Invoicing() 缓存的 _invoiceData
                    address = ExtractValue(_invoiceData, "企业地址")
                    phone = ExtractValue(_invoiceData, "企业电话")
                    bank = ExtractValue(_invoiceData, "开户银行")
                    bankAccount = ExtractValue(_invoiceData, "开户账号")
                End If

                Dim script As String = "(function() {
            const dataMap = {
                'dz': '" & address & "',
                'lxdh': '" & phone & "',
                'yhyywdmc': '" & bank & "',
                'yhzh': '" & bankAccount & "'
            };
            function fillSingleField(key, value) {
                const container = document.querySelector('.t-form-item__' + key);
                if (!container) return;
                const input = container.querySelector('input');
                if (!input) return;
                input.removeAttribute('readonly');
                input.removeAttribute('disabled');
                input.disabled = false;
                input.readOnly = false;
                input.focus();
                input.click();
                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSetter.call(input, value);
                const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
                for (let evtType of events) {
                    const event = new Event(evtType, { bubbles: true, cancelable: true });
                    Object.defineProperty(event, 'target', {writable: false, value: input});
                    input.dispatchEvent(event);
                }
                try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            }
            Object.entries(dataMap).forEach(([key, value]) => { fillSingleField(key, value); });
        })();"

                If WebViewTax.CoreWebView2 IsNot Nothing Then
                    Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
                End If
            End If
        Catch ex As Exception
            ' 错误记录
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Function

    ' 只填入发票抬头、购方税号（直接从网页获取）
    Private Async Function FillCompanyNameAndTax() As Task
        Try
            ' 获取数据并缓存
            _invoiceData = Await Invoicing()
            Dim rowCount As Integer = CInt(NumericUpDown1.Value)
            If rowCount = 0 Then

                ' 声明字符从返回结果中提取
                Dim companyName As String = ExtractValue(_invoiceData, "发票抬头")
                Dim taxNumber As String = ExtractValue(_invoiceData, "购方税号")
                ' 前置条件判断
                Dim shouldCheckNaturalPerson As Boolean = False
                If CheckBox1.Checked Then
                    If companyName.Length < 5 AndAlso String.IsNullOrEmpty(taxNumber) Then
                        shouldCheckNaturalPerson = True
                    End If
                End If
                Dim script As String = "(function() {
            const dataMap = {
                'gmfmc': """ & companyName & """,
                'gmfnsrsbh': """ & taxNumber & """
            };
            function fillSingleField(key, value) {
                const container = document.querySelector('.t-form-item__' + key);
                if (!container) return;
                const input = container.querySelector('input');
                if (!input) return;
                input.removeAttribute('readonly');
                input.removeAttribute('disabled');
                input.disabled = false;
                input.readOnly = false;
                input.focus();
                input.click();
                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSetter.call(input, value);
                const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
                for (let evtType of events) {
                    const event = new Event(evtType, { bubbles: true, cancelable: true });
                    Object.defineProperty(event, 'target', {writable: false, value: input});
                    input.dispatchEvent(event);
                }
                try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            }
            Object.entries(dataMap).forEach(([key, value]) => { fillSingleField(key, value); });
            " & (If(shouldCheckNaturalPerson, "
            // 勾选'是否开票给自然人'复选框
            const labels = document.querySelectorAll('label.t-checkbox');
            labels.forEach((label) => {
              const span = label.querySelector('.t-checkbox__label');
              const text = span ? span.textContent.trim() : '';
              if (text === '是否开票给自然人') {
                const input = label.querySelector('input.t-checkbox__former');
                if (input) {
                  input.checked = !input.checked;
                  input.dispatchEvent(new Event('change', { bubbles: true }));
                }
              }
            });
            
            // 延迟后点击'我知道了'弹窗
            setTimeout(() => {
              const footers = document.querySelectorAll('.t-dialog__footer');
              for (const footer of footers) {
                const buttons = footer.querySelectorAll('button.t-button');
                for (const btn of buttons) {
                  const span = btn.querySelector('.t-button__text');
                  if (span && span.textContent.trim() === '我知道了') {
                    btn.click();
                    return;
                  }
                }
              }
            }, 1000);
            ", "")) & "
        })();"

                If WebViewTax.CoreWebView2 IsNot Nothing Then
                    Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
                End If
            End If
        Catch ex As Exception
            ' 错误记录
        End Try
    End Function

    ' 从文本中提取特定字段的值
    Private Function ExtractValue(text As String, fieldName As String) As String
        Dim lines As String() = text.Split(New String() {vbCrLf, vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        For Each line In lines
            If line.StartsWith(fieldName & "：") OrElse line.StartsWith(fieldName & ":") Then
                Dim colonIndex = line.IndexOf("："）
                If colonIndex = -1 Then colonIndex = line.IndexOf(":")
                Dim value As String = line.Substring(colonIndex + 1).Trim()
                If value = "(空)" Then Return ""
                Return value
            End If
        Next
        Return ""
    End Function

    Private Async Sub Button8_ClickAsync(sender As Object, e As EventArgs) Handles Button8.Click '智能填入详情S
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        lblStatus.Text = "智能识别信息中..."
        Dim script1 As String = "(() => { const el = document.querySelector('.user-info .single-line-ellipsis'); return el ? el.innerText.trim() : ''; })();"
        Dim companyNameRaw = Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script1)
        Dim companyName = companyNameRaw.Trim(""""c)   ' 去掉 JSON 双引号


        Dim script2 As String = "(() => { const el = document.querySelector('[class*=\""shopName--\""]'); return el ? el.innerText.trim() : ''; })();"
        Dim companyNameRaw1 = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(script2)
        Dim companyName1 = companyNameRaw1.Trim(""""c)

        txtInvoiceInfo.Text = ""
        TextBox1.Text = ""
        Dim js As String = "
        (function(){
            var el=document.evaluate('//*[contains(text(), ""查看订单详情"")]',document,null,XPathResult.FIRST_ORDERED_NODE_TYPE,null).singleNodeValue;
            if(!el)return null;
            var box=el.closest('div'); if(!box)return null;
            var spans=box.querySelectorAll('span');
            for(var i=0;i<spans.length;i++){
                var t=spans[i].textContent.trim();
                if(/^\d{10,}$/.test(t)) return t;
            }
            return null;
        })();
        "
        Try
            ' ===== 获取订单号 =====

            Dim rawOrder = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(js)
            Dim orderId As String

            ' 先去除可能的空白字符
            Dim trimmedRawOrder As String = rawOrder.Trim().Trim(""""c)

            ' 优先使用TextBox3的内容（用户输入），排除提示文字
            If Not String.IsNullOrWhiteSpace(TextBox3.Text) AndAlso TextBox3.Text <> "请输入订单号" Then
                orderId = TextBox3.Text
            ElseIf Not String.IsNullOrWhiteSpace(trimmedRawOrder) AndAlso trimmedRawOrder <> "null" Then
                orderId = trimmedRawOrder
            Else
                orderId = String.Empty  ' 或者根据需求处理空值情况
            End If

            ' 检查是否获取到订单号
            If String.IsNullOrWhiteSpace(orderId) Then
                txtInvoiceInfo.Text = "未获取订单号"
                lblStatus.ForeColor = Color.Crimson
                lblStatus.Text = "未获取订单号"
                Return
            End If
            Dim secondaryTask As Task = Task.CompletedTask

            Await PrepareHiddenWebViews()
            _apiDataTcs = New TaskCompletionSource(Of JObject)()

            If isB2BMode Then
                _b2bInvoiceTitle = "" : _b2bTaxNo = "" : _b2bRegAddress = ""
                _b2bBankName = "" : _b2bBankAccount = "" : _b2bRegPhone = ""
                _b2bCurrentOrderId = orderId
                _invoiceTcs = New TaskCompletionSource(Of Boolean)()
                secondaryTask = _invoiceTcs.Task
            Else
                ' 【修复】提前在这里判断并启动税号验证流程，而不是等订单数据处理完之后
                If Not String.IsNullOrWhiteSpace(TextBox3.Text) AndAlso TextBox3.Text <> "请输入订单号" Then
                    ' 手动输入订单号：不执行
                ElseIf Not String.IsNullOrWhiteSpace(trimmedRawOrder) AndAlso trimmedRawOrder <> "null" Then
                    secondaryTask = FillTaobaoCompanyInfoAsync()   ' 提前发起
                End If
            End If

            Dim taskClick As Task(Of String) = Task.FromResult("跳过")
            If Not isB2BMode Then
                Await Task.WhenAny(secondaryTask, Task.Delay(8000))   ' 现在这里才是真正在等验证流程
                taskClick = RunWangWangLogicAsync(orderId)
            End If

            Dim targetUrl As String = String.Empty
            If isB2BMode Then
                targetUrl = $"https://air.1688.com/app/ctf-page/trade-order-detail/index.html?orderId={orderId}&to=invoiceInfoTab"
            ElseIf shangjiang = "0" Then
                targetUrl = $"https://trade.taobao.com/detail/orderDetailQianNiu.htm?bizOrderId={orderId}&sifg=0&isQnNew=true&isHideNick=true"
            Else
                targetUrl = $"https://trade.taobao.com/trade/detail/trade_order_detail_qian_niu.htm?bizOrderId={orderId}&isQnNew=true"
            End If
            dataWebView.CoreWebView2.Navigate(targetUrl)   ' 现在才真正推迟到验证流程跑完(或8秒超时)之后

            Dim orderTask As Task(Of JObject) = _apiDataTcs.Task
            Dim completedOrderTask = Await Task.WhenAny(orderTask, Task.Delay(10000))

            If completedOrderTask Is orderTask AndAlso orderTask.Status = TaskStatus.RanToCompletion Then
                Dim interceptedData = orderTask.Result
                Await ProcessExtractedData(interceptedData, orderId)

                If isB2BMode Then
                    Await Task.WhenAny(secondaryTask, Task.Delay(10000))
                    Await FillCompanyFrom1688()
                    Await Butt_yz()
                    Await FillCompanyInformation()
                End If
                ' 非B2B模式：secondaryTask已经在前面启动并等待过了，这里不需要再处理
            Else
                lblStatus.Text = "数据抓取超时"
            End If
            ' 根据公司名称执行额外动作
            'If companyName = "" Then
            '    Await Button23Core()
            'ElseIf companyName = "" Then
            '    Await Button24Core()
            'End If
            ' 确保点击任务也完成了
            ' 4. 【最后确认旺旺】此时旺旺任务可能也跑完了
            Dim clickStatus = Await taskClick
            'If clickStatus = "成功" Then
            '    lblStatus.Text = "旺旺点击成功"
            'Else
            '    lblStatus.Text = "旺旺点击失败"
            'End If
            'lblStatus.Text = "旺旺唤起状态: " & clickStatus
        Catch ex As Exception
            lblStatus.Text = "数据解析失败: " & ex.Message
        End Try


        ' 公司名为空或未登录时，不进行匹配检查
        If String.IsNullOrEmpty(companyName) OrElse companyName = "" Then
            Return
        End If
        Dim isMatch As Boolean = False

        If companyName = config.Company1 AndAlso companyName1 = config.Shop1 Then
            isMatch = True
        ElseIf companyName = config.Company2 AndAlso companyName1 = config.Shop2 Then
            isMatch = True
        ElseIf companyName = config.Company3 AndAlso companyName1 = config.Shop3 Then
            isMatch = True
        ElseIf companyName = config.Company4 AndAlso companyName1 = config.Shop4 Then
            isMatch = True
        End If
        If isB2BMode Then Return
        If Not isMatch Then
            MessageBox.Show("网站不匹配", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Else
            '' 匹配时的操作（可选，例如继续后续逻辑）
            'MessageBox.Show("匹配成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If

    End Sub
    Private Async Function FillTaobaoCompanyInfoAsync() As Task
        Await FillCompanyNameAndTax() '填入发票抬头和税号
        Await Butt_yz()
        Await FillCompanyInformation() '填入企业地址、企业电话、开户银行、开户账号
    End Function
    Private Async Function FillCompanyFrom1688() As Task
        Try
            If String.IsNullOrEmpty(_b2bInvoiceTitle) Then Return
            Dim script As String = "(function() { const dataMap = { 'gmfmc': """ & _b2bInvoiceTitle & """, 'gmfnsrsbh': """ & _b2bTaxNo & """ }; function fillSingleField(key, value) { const container = document.querySelector('.t-form-item__' + key); if (!container) return; const input = container.querySelector('input'); if (!input) return; input.removeAttribute('readonly'); input.removeAttribute('disabled'); input.disabled = false; input.readOnly = false; input.focus(); input.click(); const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set; nativeSetter.call(input, value); const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur']; for (let evtType of events) { const event = new Event(evtType, { bubbles: true, cancelable: true }); Object.defineProperty(event, 'target', {writable: false, value: input}); input.dispatchEvent(event); } try { input.select(); document.execCommand('insertText', false, value); } catch (e) {} } Object.entries(dataMap).forEach(([key, value]) => { fillSingleField(key, value); }); })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
        Catch
        End Try
    End Function
    ''' <summary>标题是否包含 keyStr 中逗号分隔的任意一个词</summary>
    Private Function ContainsAny(title As String, keyStr As String) As Boolean
        If String.IsNullOrWhiteSpace(keyStr) Then Return False
        For Each kw In keyStr.Split(","c)
            Dim word = kw.Trim()
            If word <> "" AndAlso title.Contains(word) Then Return True
        Next
        Return False
    End Function
    Private Async Function ProcessExtractedData(data As JObject, orderId As String) As Task
        Try
            ' ========== 公共变量（三平台统一）==========
            Dim userPaid As String = "0.00"
            Dim payTime As String = "无付款时间"
            Dim laterPay As String = "0.00"
            Dim firstTitle As String = ""
            Dim colorRaw As String = ""
            Dim code As String = String.Empty
            Dim totalQty As Double = 0

            If isB2BMode Then
                ' ==================== 1688 分支 ====================
                ' 定位 data.data.model（data 可能是对象也可能是字符串）
                Dim model As JObject = Nothing
                Dim dataNode = data("data")
                If dataNode IsNot Nothing Then
                    If dataNode.Type = JTokenType.String Then
                        Dim inner = JObject.Parse(dataNode.ToString())
                        model = TryCast(inner("model"), JObject)
                    Else
                        model = TryCast(dataNode("model"), JObject)
                    End If
                End If
                If model Is Nothing Then model = TryCast(data("model"), JObject)
                If model Is Nothing Then Throw New Exception("1688响应中未找到model节点")

                ' 实付款（单位：分 → 元）
                Dim sumPayCent As Long = 0
                Long.TryParse(model("sumPaymentWithoutCouponFee")?.ToString(), sumPayCent)
                userPaid = (sumPayCent / 100D).ToString("F2")

                ' 付款时间
                Dim gmtPay = model("gmtPayment")?.ToString()
                If Not String.IsNullOrWhiteSpace(gmtPay) Then payTime = gmtPay

                ' 商品行：标题 / 商家编码(productNumber) / 数量累加 / 规格
                Dim entries = model("orderEntries")
                If entries IsNot Nothing AndAlso entries.HasValues Then
                    For Each entry In entries
                        If String.IsNullOrEmpty(firstTitle) Then
                            firstTitle = entry("productName")?.ToString()
                            code = entry("productNumber")?.ToString()
                        End If
                        Dim q As Double = 0
                        Double.TryParse(entry("quantity")?.ToString(), q)
                        totalQty += q
                    Next
                    Try
                        colorRaw = entries(0)("specInfoModel")("specItems")(0)("specValue")?.ToString()
                    Catch
                    End Try
                End If

            Else
                ' ==================== 原淘宝/天猫解析（保持不动）====================
                Dim mainOrder = data("mainOrder")
                Dim priceModule = data("soldDetailPriceModule")?(0)?("params")
                Dim priceRaw As String = priceModule?("price")?.ToString()
                If String.IsNullOrWhiteSpace(priceRaw) OrElse priceRaw.Trim() = "￥" Then
                    userPaid = "0.00"
                Else
                    userPaid = priceRaw.Replace("￥", "").Trim()
                End If

                Try
                    Dim lines = mainOrder("orderInfo")?("lines")
                    If lines IsNot Nothing Then
                        For Each lineGroup In lines
                            Dim content = lineGroup("content")
                            If content IsNot Nothing Then
                                For Each item In content
                                    Dim itemName = item("value")?("name")?.ToString()
                                    If Not String.IsNullOrEmpty(itemName) AndAlso itemName.Contains("付款时间") Then
                                        payTime = item("value")?("value")?.ToString()
                                        Exit For
                                    End If
                                Next
                            End If
                            If payTime <> "无付款时间" Then Exit For
                        Next
                    End If
                Catch ex As Exception
                    payTime = "时间提取出错"
                End Try

                Try
                    Dim priceCompose = data("soldDetailPriceModule")(0)("params")("compose")
                    If priceCompose IsNot Nothing Then
                        For Each item In priceCompose
                            If item("label")?.ToString().Contains("先用后付") Then
                                laterPay = item("value")?.ToString().Replace("￥", "")
                                Exit For
                            End If
                        Next
                    End If
                Catch
                End Try

                Dim subOrders = mainOrder("subOrders")
                If subOrders IsNot Nothing Then
                    firstTitle = subOrders(0)("itemInfo")("title")?.ToString()
                    Try
                        colorRaw = subOrders(0)("itemInfo")("skuText")(0)("content")(0)("value")("value")?.ToString()
                    Catch
                    End Try
                    For Each subOrder In subOrders
                        totalQty += Val(subOrder("quantity")?.ToString())
                    Next
                End If

                If shangjiang = "1" Then
                    SearchTaobaoProducts(firstTitle)
                    code = Await WaitForJSMessageAsync()
                ElseIf shangjiang = "0" Then
                    Dim extraInfo = subOrders(0)("itemInfo")("extra")
                    If extraInfo IsNot Nothing Then
                        For Each info In extraInfo
                            If info("name").ToString() = "商家编码" Then
                                code = info("value").ToString()
                                Exit For
                            End If
                        Next
                    End If
                End If
            End If
            Dim projectName As String = config.DefaultSpecOption

            If ContainsAny(firstTitle, config.Keywords1) Then
                projectName = config.Spec1
            ElseIf ContainsAny(firstTitle, config.Keywords2) Then
                projectName = config.Spec2
            ElseIf ContainsAny(firstTitle, config.Keywords3) Then
                projectName = config.Spec3
            ElseIf ContainsAny(firstTitle, config.Keywords4) Then
                projectName = config.Spec4
            ElseIf ContainsAny(firstTitle, config.Keywords5) Then
                projectName = config.Spec5
            ElseIf ContainsAny(firstTitle, config.Keywords6) Then
                projectName = config.Spec6
            ElseIf ContainsAny(firstTitle, config.Keywords7) Then
                projectName = config.Spec7
            End If

            ' 6. 换算逻辑：半米判定
            Dim lengthFactor As Double = 1.0
            ' 如果规格里有“45CM*50CM”或者“半米”字样
            If colorRaw.Contains("半米") OrElse colorRaw.Contains("0.5米") Then
                lengthFactor = 0.5
            End If
            ' 邮费逻辑：除15的倍数
            Dim totalMeter As String
            If firstTitle.Contains("邮费") Then
                If userPaid = "0.00" Then
                    totalMeter = Math.Round(laterPay / 15, 0, MidpointRounding.AwayFromZero).ToString()
                Else
                    totalMeter = Math.Round(userPaid / 15, 0, MidpointRounding.AwayFromZero).ToString()
                End If

            Else
                totalMeter = (totalQty * lengthFactor).ToString()
            End If

            ' 7. 获取发票金额 (从界面现有值提取)
            Dim companyjen As String = ExtractValue(projectNamex.Text, "发票金额").Replace("￥", "").Trim()

            ' 8. 更新 UI (跨线程安全)
            Me.Invoke(Sub()
                          txtInvoiceInfo.Text = $"订单号: {orderId}" & vbCrLf &
                                 $"付款时间: {payTime}" & vbCrLf & vbCrLf &
                                 $"标题: {firstTitle}" & vbCrLf & vbCrLf &
                                 $"商家编码 {code}" & vbCrLf & vbCrLf &
                                 $"颜色分类: {colorRaw}" & vbCrLf & vbCrLf &
                                 $"总数量: {totalQty}" & vbCrLf & vbCrLf &
                                 $"实付款: {userPaid} 元" & vbCrLf &
                                 $"先享后付款: {laterPay} 元"
                      End Sub)
            '优先使用TextBox3的内容（用户输入），排除提示文字
            Dim txt As String
            If Not String.IsNullOrWhiteSpace(TextBox3.Text) AndAlso TextBox3.Text <> "请输入订单号" Then
                If userPaid = "0.00" Then
                    txt = laterPay
                Else
                    txt = userPaid
                End If
            Else
                txt = companyjen
            End If
            TextBox1.Text = $"{projectName}{vbCrLf}{code}{vbCrLf}{totalMeter}{vbCrLf}{txt}"
            ' 9. 执行自动开票填入
            Dim rowCount As Integer = CInt(NumericUpDown1.Value)
            If rowCount > 0 Then

                ' 点击增行
                Dim jsAddRow = "(function() {
                    var btns = Array.from(document.querySelectorAll('button'));
                    var addBtns = btns.filter(function(b) { return b.textContent.trim() === '增行'; });
                    var btn = addBtns[0];
                    var el = btn;
                    while (el) {
                        if (el.__vue__) {
                            el.__vue__.$emit('click');
                            break;
                        }
                        el = el.parentElement;
                    }
                })()"
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsAddRow)
            End If

            Await Tianru(projectName, code, totalMeter, txt, rowCount, rowCount = 0)


            If rowCount > 0 Then
                lblStatus.ForeColor = Color.LimeGreen
                lblStatus.Text = "填写完成"
            End If
        Catch ex As Exception
            lblStatus.Text = "数据解析失败: " & ex.Message
        End Try

        Return
    End Function

    Private Async Function Tianru(productCode As String, guige As String, totalQuantity As String, companyjen As String, Optional rowIndex As Integer = 0, Optional isFirstRow As Boolean = True) As Task
        Try
            ' 这里的 productCode 传入格式需为: "“针织烫金布料”，或“绣花布料”，或“雪纺烫金”，或“褶皱面料”，或“提花面料”，或“印花面料”，或“花瓣”"
            ' 第一步: 点击更多按钮
            Dim dwValue As String = ""
            If config.ConditionList.Contains(guige) Then
                dwValue = config.TriggerUnit
            End If
            Dim jsPayee As String = config.Payee
            Dim jsReviewer As String = config.Reviewer
            Dim jsStep1 As String = "(function() {
            var moreBtn = document.querySelector('.hwhyslwfwmc_" & rowIndex & "');
            if (moreBtn) {
                var btn = moreBtn.parentElement.querySelector('button');
                var el = btn;
                while (el) {
                    if (el.__vue__) { el.__vue__.$emit('click'); return 'clicked'; }
                    el = el.parentElement;
                }
            }
            return 'not found';
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsStep1)

            ' 等待表格渲染
            Await Task.Delay(800)

            ' 第二步: 选行 + 等回填 + 填充字段
            Dim jsStep2 As String = "(function() {
            // ========== 通用填充函数 ==========
            function fillInput(input, value) {
                if (!input) return false;
                input.removeAttribute('readonly');
                input.removeAttribute('disabled');
                input.disabled = false;
                input.readOnly = false;
                input.focus();
                input.click();
                var nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeSetter.call(input, value);
                var events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
                for (var j = 0; j < events.length; j++) {
                    var evt = new Event(events[j], { bubbles: true, cancelable: true });
                    Object.defineProperty(evt, 'target', { writable: false, value: input });
                    input.dispatchEvent(evt);
                }
                try { input.select(); document.execCommand('insertText', false, value); } catch(e) {}
                input.blur();
                input.dispatchEvent(new Event('blur', { bubbles: true, cancelable: true }));
                document.body.click();
                return true;
            }

            // ========== ★ 表格字段：通过 Vue 实例直接设置，然后触发 blur 和点击空白 ==========
            function fillTableCells(ggxhVal, spslVal, jeVal) {
                var tbody = document.querySelector('.t-table__content .t-table__body');
                if (!tbody) return false;
                var table = tbody.closest('table');
                if (!table) return false;
                var ths = Array.from(table.querySelectorAll('thead th'));
                var spslColIdx = -1;
                var jeColIdx = -1;
                for (var i = 0; i < ths.length; i++) {
                    var colkey = ths[i].getAttribute('data-colkey');
                    if (colkey === 'spsl') spslColIdx = i;
                    if (colkey === 'je') jeColIdx = i;
                }
                if (spslColIdx < 0 || jeColIdx < 0) return false;

                var rows = tbody.querySelectorAll('tr');
                var row = rows[" & rowIndex & "];
                if (!row) return false;
                var tds = Array.from(row.querySelectorAll('td'));
                var spslTd = tds[spslColIdx];
                var spslInput = spslTd ? spslTd.querySelector('input') : null;
                if (!spslInput) return false;

                // 获取 Vue 实例并直接修改数据
                var el = spslInput;
                var vueInstance = null;
                for (var i = 0; i < 6; i++) {
                    el = el.parentElement;
                    if (!el) break;
                    if (el.__vue__ && el.__vue__.$props && el.__vue__.$props.row) {
                        vueInstance = el.__vue__; break;
                    }
                }
                if (!vueInstance) return false;
                vueInstance.$props.row.ggxh = ggxhVal;
                if ('" & dwValue & "') vueInstance.$props.row.dw = '" & dwValue & "';
                vueInstance.$props.row.spsl = spslVal;
                vueInstance.$props.row.je = jeVal;
                vueInstance.$forceUpdate();

                // 延迟触发 blur 和点击空白，确保数据已渲染
                setTimeout(function() {
                    var currentRow = tbody.querySelectorAll('tr')[" & rowIndex & "];
                    if (!currentRow) return;
                    var currentTds = Array.from(currentRow.querySelectorAll('td'));
                    var spslInput2 = currentTds[spslColIdx] ? currentTds[spslColIdx].querySelector('input') : null;
                    var jeInput2 = currentTds[jeColIdx] ? currentTds[jeColIdx].querySelector('input') : null;
                    function triggerBlur(el) {
                        if (!el) return;
                        el.focus();
                        el.dispatchEvent(new Event('focus', { bubbles: true }));
                        el.dispatchEvent(new Event('blur', { bubbles: true }));
                    }
                    if (spslInput2) triggerBlur(spslInput2);
                    if (jeInput2) triggerBlur(jeInput2);
                    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true, clientX: 0, clientY: 0 }));
                }, 300);
                return true;
            }

            // ========== ★ 表单字段：通过 label 文字定位父容器 ==========
            function fillFormField(labelText, value) {
                var labels = document.querySelectorAll('.t-form__label label');
                for (var i = 0; i < labels.length; i++) {
                    if (labels[i].textContent.trim() === labelText) {
                        var formItem = labels[i].closest('.t-form__item');
                        if (!formItem) {
                            formItem = labels[i].closest('[class*=""t-form-item""]');
                        }
                        if (!formItem) continue;
                        var inp = formItem.querySelector('input.t-input__inner');
                        if (inp) return fillInput(inp, value);
                    }
                }
                return false;
            }

            // ========== 选行 ==========
            var rows = document.querySelectorAll('.t-table__body tr');
            for (var i = 0; i < rows.length; i++) {
                var cell = rows[i].querySelector('.t-table__cell--fixed-left-last .t-text-ellipsis');
                if (cell && cell.textContent.trim() === '" & productCode & "') {
                    var btn = rows[i].querySelector('.primary-button__text');
                    if (btn) btn.click();
                    break;
                }
            }

            // ========== 等待回填完成后，填充表格和表单字段，最后聚焦到购方税号输入框 ==========
            setTimeout(function() {
                fillTableCells('" & guige & "', '" & totalQuantity & "', '" & companyjen & "');
                fillTableCells('" & guige & "', '" & totalQuantity & "', '" & companyjen & "');
                if (" & rowIndex & " === 0) {
                    fillFormField('收款人', '" & jsPayee & "');
                    fillFormField('复核人', '" & jsReviewer & "');
                }
            }, 800);
            // 独立延迟聚焦到购方税号输入框，确保表格内的 blur 和点击空白已经执行完毕（800 + 100 + 余量）
            setTimeout(function() {
                var container = document.querySelector('.t-form-item__gmfnsrsbh');
                if (container) {
                    var input = container.querySelector('input');
                    if (input) {
                        input.focus();
                        input.click();
                    }
                }
            }, 1000);
        })();"
            Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsStep2)
            'lblStatus.Text = "自动选择并填充中..."

        Catch ex As Exception
            lblStatus.Text = "脚本执行失败: " & ex.Message
        End Try
    End Function


    ''' <summary>
    ''' 点击旺旺
    ''' </summary>
    Private Async Function RunWangWangLogicAsync(orderId As String) As Task(Of String)
        Await InitHiddenWebView2()
        Dim clickTcs As New TaskCompletionSource(Of String)()

        Dim handler As EventHandler(Of CoreWebView2NavigationCompletedEventArgs) = Nothing
        handler = Async Sub(s, e)
                      RemoveHandler logicWebView.CoreWebView2.NavigationCompleted, handler

                      If Not e.IsSuccess Then
                          clickTcs.TrySetResult("页面加载失败")
                          Return
                      End If

                      ' --- 核心改进：轮询寻找按钮 ---
                      Dim found As Boolean = False
                      Dim retryCount As Integer = 0
                      Dim maxRetries As Integer = 80 ' 最多等 8秒 (80 * 100ms)

                      While Not found AndAlso retryCount < maxRetries
                          ' JS 逻辑：寻找并点击。如果点到了返回 true，否则 false
                          Dim jsPolling As String = "
            (function(){
                let btn = Array.from(document.querySelectorAll('a'))
                               .find(a => (a.textContent || '').trim() === '旺旺在线' || 
                                          a.className.includes('ww-inline'));
                if (btn) {
                    btn.click();
                    return true;
                }
                return false;
            })();"

                          Dim result = Await logicWebView.CoreWebView2.ExecuteScriptAsync(jsPolling)

                          If result = "true" Then
                              found = True
                              clickTcs.TrySetResult("成功")
                          Else
                              retryCount += 1
                              Await Task.Delay(100) ' 每半秒找一次
                          End If
                      End While

                      If Not found Then clickTcs.TrySetResult("超时未找到")
                  End Sub

        AddHandler logicWebView.CoreWebView2.NavigationCompleted, handler
        logicWebView.CoreWebView2.Navigate($"https://qn.taobao.com/home.htm/trade-platform/tp/detail?bizOrderId={orderId}")

        Return Await clickTcs.Task
    End Function
    ' 等待JS消息的异步方法
    Private Async Function WaitForJSMessageAsync() As Task(Of String)
        Dim tcs As New TaskCompletionSource(Of String)()

        ' 临时的事件处理程序
        Dim handler As EventHandler(Of CoreWebView2WebMessageReceivedEventArgs) =
        Sub(sender, e)
            Dim message As String = e.TryGetWebMessageAsString()
            ' 移除事件处理器，避免重复
            RemoveHandler WebViewTaoBao.CoreWebView2.WebMessageReceived, handler
            tcs.SetResult(message)
        End Sub

        ' 注册事件处理器
        AddHandler WebViewTaoBao.CoreWebView2.WebMessageReceived, handler

        ' 设置超时（例如5秒）
        Dim timeoutTask As Task = Task.Delay(5000)
        Dim completedTask As Task = Await Task.WhenAny(tcs.Task, timeoutTask)

        ' 如果超时，返回空字符串
        If completedTask Is timeoutTask Then
            RemoveHandler WebViewTaoBao.CoreWebView2.WebMessageReceived, handler
            Return ""
        End If

        Return Await tcs.Task
    End Function
    ''' <summary>
    ''' 获取商家编码
    ''' </summary>
    Private Async Sub SearchTaobaoProducts(searchKeyword As String)
        Dim escapedKeyword As String = searchKeyword.Replace("\", "\\").Replace("'", "\'").Replace("""", "\""")

        Dim script As String = "
(async function() {
    try {
        console.log('开始执行搜索...');
        
        function md5(string) {
            function md5cycle(x, k) {
                var a = x[0], b = x[1], c = x[2], d = x[3];
                a = ff(a, b, c, d, k[0], 7, -680876936);
                d = ff(d, a, b, c, k[1], 12, -389564586);
                c = ff(c, d, a, b, k[2], 17, 606105819);
                b = ff(b, c, d, a, k[3], 22, -1044525330);
                a = ff(a, b, c, d, k[4], 7, -176418897);
                d = ff(d, a, b, c, k[5], 12, 1200080426);
                c = ff(c, d, a, b, k[6], 17, -1473231341);
                b = ff(b, c, d, a, k[7], 22, -45705983);
                a = ff(a, b, c, d, k[8], 7, 1770035416);
                d = ff(d, a, b, c, k[9], 12, -1958414417);
                c = ff(c, d, a, b, k[10], 17, -42063);
                b = ff(b, c, d, a, k[11], 22, -1990404162);
                a = ff(a, b, c, d, k[12], 7, 1804603682);
                d = ff(d, a, b, c, k[13], 12, -40341101);
                c = ff(c, d, a, b, k[14], 17, -1502002290);
                b = ff(b, c, d, a, k[15], 22, 1236535329);
                a = gg(a, b, c, d, k[1], 5, -165796510);
                d = gg(d, a, b, c, k[6], 9, -1069501632);
                c = gg(c, d, a, b, k[11], 14, 643717713);
                b = gg(b, c, d, a, k[0], 20, -373897302);
                a = gg(a, b, c, d, k[5], 5, -701558691);
                d = gg(d, a, b, c, k[10], 9, 38016083);
                c = gg(c, d, a, b, k[15], 14, -660478335);
                b = gg(b, c, d, a, k[4], 20, -405537848);
                a = gg(a, b, c, d, k[9], 5, 568446438);
                d = gg(d, a, b, c, k[14], 9, -1019803690);
                c = gg(c, d, a, b, k[3], 14, -187363961);
                b = gg(b, c, d, a, k[8], 20, 1163531501);
                a = gg(a, b, c, d, k[13], 5, -1444681467);
                d = gg(d, a, b, c, k[2], 9, -51403784);
                c = gg(c, d, a, b, k[7], 14, 1735328473);
                b = gg(b, c, d, a, k[12], 20, -1926607734);
                a = hh(a, b, c, d, k[5], 4, -378558);
                d = hh(d, a, b, c, k[8], 11, -2022574463);
                c = hh(c, d, a, b, k[11], 16, 1839030562);
                b = hh(b, c, d, a, k[14], 23, -35309556);
                a = hh(a, b, c, d, k[1], 4, -1530992060);
                d = hh(d, a, b, c, k[4], 11, 1272893353);
                c = hh(c, d, a, b, k[7], 16, -155497632);
                b = hh(b, c, d, a, k[10], 23, -1094730640);
                a = hh(a, b, c, d, k[13], 4, 681279174);
                d = hh(d, a, b, c, k[0], 11, -358537222);
                c = hh(c, d, a, b, k[3], 16, -722521979);
                b = hh(b, c, d, a, k[6], 23, 76029189);
                a = hh(a, b, c, d, k[9], 4, -640364487);
                d = hh(d, a, b, c, k[12], 11, -421815835);
                c = hh(c, d, a, b, k[15], 16, 530742520);
                b = hh(b, c, d, a, k[2], 23, -995338651);
                a = ii(a, b, c, d, k[0], 6, -198630844);
                d = ii(d, a, b, c, k[7], 10, 1126891415);
                c = ii(c, d, a, b, k[14], 15, -1416354905);
                b = ii(b, c, d, a, k[5], 21, -57434055);
                a = ii(a, b, c, d, k[12], 6, 1700485571);
                d = ii(d, a, b, c, k[3], 10, -1894986606);
                c = ii(c, d, a, b, k[10], 15, -1051523);
                b = ii(b, c, d, a, k[1], 21, -2054922799);
                a = ii(a, b, c, d, k[8], 6, 1873313359);
                d = ii(d, a, b, c, k[15], 10, -30611744);
                c = ii(c, d, a, b, k[6], 15, -1560198380);
                b = ii(b, c, d, a, k[13], 21, 1309151649);
                a = ii(a, b, c, d, k[4], 6, -145523070);
                d = ii(d, a, b, c, k[11], 10, -1120210379);
                c = ii(c, d, a, b, k[2], 15, 718787259);
                b = ii(b, c, d, a, k[9], 21, -343485551);
                x[0] = add32(a, x[0]);
                x[1] = add32(b, x[1]);
                x[2] = add32(c, x[2]);
                x[3] = add32(d, x[3]);
            }
            function cmn(q, a, b, x, s, t) {
                a = add32(add32(a, q), add32(x, t));
                return add32((a << s) | (a >>> (32 - s)), b);
            }
            function ff(a, b, c, d, x, s, t) { return cmn((b & c) | ((~b) & d), a, b, x, s, t); }
            function gg(a, b, c, d, x, s, t) { return cmn((b & d) | (c & (~d)), a, b, x, s, t); }
            function hh(a, b, c, d, x, s, t) { return cmn(b ^ c ^ d, a, b, x, s, t); }
            function ii(a, b, c, d, x, s, t) { return cmn(c ^ (b | (~d)), a, b, x, s, t); }
            function md51(s) {
                var n = s.length, state = [1732584193, -271733879, -1732584194, 271733878], i;
                for (i = 64; i <= s.length; i += 64) md5cycle(state, md5blk(s.substring(i - 64, i)));
                s = s.substring(i - 64);
                var tail = [0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0];
                for (i = 0; i < s.length; i++) tail[i>>2] |= s.charCodeAt(i) << ((i%4) << 3);
                tail[i>>2] |= 0x80 << ((i%4) << 3);
                if (i > 55) {
                    md5cycle(state, tail);
                    for (i = 0; i < 16; i++) tail[i] = 0;
                }
                tail[14] = n*8;
                md5cycle(state, tail);
                return state;
            }
            function md5blk(s) {
                var md5blks = [], i;
                for (i = 0; i < 64; i += 4) {
                    md5blks[i>>2] = s.charCodeAt(i) + (s.charCodeAt(i+1) << 8) + (s.charCodeAt(i+2) << 16) + (s.charCodeAt(i+3) << 24);
                }
                return md5blks;
            }
            var hex_chr = '0123456789abcdef'.split('');
            function rhex(n) {
                var s='', j=0;
                for(; j<4; j++) s += hex_chr[(n >> (j * 8 + 4)) & 0x0F] + hex_chr[(n >> (j * 8)) & 0x0F];
                return s;
            }
            function hex(x) {
                for (var i=0; i<x.length; i++) x[i] = rhex(x[i]);
                return x.join('');
            }
            function add32(a, b) { return (a + b) & 0xFFFFFFFF; }
            return hex(md51(unescape(encodeURIComponent(string))));
        }

        const getCookie = (name) => {
            const cookies = document.cookie.split('; ');
            for (let cookie of cookies) {
                const [key, value] = cookie.split('=');
                if (key === name) return value;
            }
            return null;
        };

        console.log('获取Cookie...');
        const tokenCookie = getCookie('_m_h5_tk');
        if (!tokenCookie) {
            console.error('令牌无效');
            return '错误:令牌无效';
        }

        const token = tokenCookie.split('_')[0];
        const timestamp = Date.now();
        const appKey = '12574478';
        
        console.log('Token:', token);
        console.log('Timestamp:', timestamp);

        const data = {
            url: '/taobao/manager/table.htm',
            jsonBody: JSON.stringify({
                tab: 'all',
                pagination: { current: 1, pageSize: 20 },
                filtertab: '',
                filter: {
                    queryTitle: '" & escapedKeyword & "'
                },
                table: {}
            })
        };
        const dataStr = JSON.stringify(data);
        const sign = md5(`${token}&${timestamp}&${appKey}&${dataStr}`);
        
        console.log('Sign:', sign);

        const url = `https://h5api.m.taobao.com/h5/mtop.taobao.sell.pc.manage.async/1.0/?jsv=2.6.1&appKey=${appKey}&t=${timestamp}&sign=${sign}&api=mtop.taobao.sell.pc.manage.async&v=1.0&ttid=11320%40taobao_WEB_9.9.99&type=originaljson&dataType=json`;

        console.log('开始请求...');
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: 'data=' + encodeURIComponent(dataStr),
            credentials: 'include'
        });

        console.log('请求完成，解析响应...');
        const json = await res.json();
        console.log('响应数据:', json);
        
        if (json.ret && json.ret[0] === 'SUCCESS::调用成功') {
            console.log('调用成功');
            const innerResult = JSON.parse(json.data.result);
            const dataSource = innerResult.data?.table?.dataSource || [];
            
            console.log('找到商品数量:', dataSource.length);
            
            if (dataSource.length === 0) return '未找到商品';
            
            const item = dataSource[0];
            let outerCode = '涤纶布';
            
            console.log('商品数据:', item);
            
            if (item.itemDesc && item.itemDesc.desc) {
                const descObj = item.itemDesc.desc.find(d => d.text && d.text.includes('编码:'));
                if (descObj) {
                    outerCode = descObj.text.replace('编码:', '').trim();
                    console.log('从itemDesc找到编码:', outerCode);
                }
            }
            
            if (outerCode === '涤纶布' && item.columns) {
                const codeCol = item.columns.find(c => c.title === '商家编码' || c.key === 'outerId');
                if (codeCol) {
                    outerCode = codeCol.value;

                    console.log('从columns找到编码:', outerCode);
                }
            }
           window.chrome.webview.postMessage(outerCode);    

        } else {
        }
    } catch (e) {
    }
})();"
        Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(script)
    End Sub
    Private Async Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Dim directoryPath As String = config.SelectedPath.Replace("\\", "\")
        Dim pdfFiles = Directory.GetFiles(directoryPath, "*.pdf").
                   OrderByDescending(Function(f) File.GetLastWriteTime(f)).ToList()

        If pdfFiles.Count = 0 Then
            lblStatus.Text = "未找到PDF文件"
            Return
        End If
        Await Button9Core(pdfFiles(0))
        Me.ActiveControl = Nothing
        Me.Focus()
    End Sub
    Private Async Sub Button22_Click(sender As Object, e As EventArgs) Handles Button22.Click
        '清除
        TextBox3.ForeColor = Color.Gray
        TextBox3.Text = "请输入订单号"
        NumericUpDown1.Value = 0

        'lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Await Button22Core()
        'lblStatus.Text = "发票开具"
    End Sub
    Private Async Function Button22Core() As Task
        Dim jsClick As String = "(function() {
    var btns = Array.from(document.querySelectorAll('button'));
    var btn = btns.find(function(b) { return b.innerText.trim() === '发票开具'; });
    if (!btn) return;
    var el = btn;
    while (el) {
        if (el.__vue__) {
            el.__vue__.$emit('click');
            break;
        }
        el = el.parentElement;
    }
})();"

        Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsClick)
    End Function
    Private Async Function Button9Core(pdfFilePath As String) As Task
        Dim script As String = "
            (function() {
                function getVal(id, labelText) {
                    var value = '';
                    
                    
                    // 方法2：如果没找到，通过label文本查找
                    if (!value && labelText) {
                        var labels = document.querySelectorAll('label');
                        for (var i = 0; i < labels.length; i++) {
                            if (labels[i].textContent.trim() === labelText) {
                                var row = labels[i].closest('[role=""row""]');
                                if (row) {
                                    var valueSpan = row.querySelector('span.invoiceDetail_formValue__esg\\+o');
                                    if (!valueSpan) {
                                        valueSpan = row.querySelector('[class*=""invoiceDetail_formValue""]');
                                    }
                                    if (valueSpan) {
                                        value = valueSpan.textContent.trim();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    return value || '';
                }
    
                var result = [];
                result.push('' + getVal('payerName', '发票抬头'));                
                return result.join('\r\n');
            })();"

        Dim jsonResult As String = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(script)
        If Not String.IsNullOrEmpty(jsonResult) Then
            jsonResult = jsonResult.Trim(ControlChars.Quote)
        End If

        Await Task.Delay(100)   ' Thread.Sleep 改掉，别卡 UI

        ' 直接用传入的路径，不再 Directory.GetFiles 重扫
        Dim Vss As String = pdfFilePath.Split("_"c)(2)
        Dim vssClean = Vss.Replace("（个体工商户）", "").Replace("(个体工商户)", "").Replace("（个人）", "").Replace("(个人)", "")
        Dim jsonClean = jsonResult.Replace("（个体工商户）", "").Replace("(个体工商户)", "").Replace("（个人）", "").Replace("(个人)", "")

        If vssClean.Replace("(", "").Replace(")", "").Replace("（", "").Replace("）", "") <>
       jsonClean.Replace("(", "").Replace(")", "").Replace("（", "").Replace("）", "") Then
            lblStatus.Text = "PDF名称不匹配,请手动选择！"
        Else
            Openwenj()
            Await Task.Delay(500)
            Dim ok As Boolean = Await SetFileInputAsync(pdfFilePath, "#tsfFileName")
            If ok Then
                lblStatus.Text = "文件已自动上传，等待解析..."
                Dim parsed = Await WaitForInvoiceParsedAsync()
                If parsed Then
                    lblStatus.Text = "发票信息已解析完成"
                    Await ClickCompleteInvoiceAsync()
                Else
                    lblStatus.Text = "解析超时，请手动检查发票信息后点击完成开票"
                End If
            Else
                lblStatus.Text = "未找到文件，可能弹窗还没渲染，需要增加等待时间"
            End If
        End If

        Me.ActiveControl = Nothing
        Me.Focus()
    End Function
    Private Async Function WaitForInvoiceParsedAsync(Optional maxWaitMs As Integer = 4000) As Task(Of Boolean)
        Dim checkScript = "
    (function() {
        var noEl = document.getElementById('invoiceNo');
        var dateWrapper = document.getElementById('invoiceDate');
        var dateEl = dateWrapper ? dateWrapper.querySelector('input') : null;

        var noVal = noEl ? noEl.value.trim() : '';
        var dateVal = dateEl ? dateEl.value.trim() : '';

        return (noVal.length > 0 && dateVal.length > 0) ? 'true' : 'false';
    })();
"
        Dim elapsed As Integer = 0
        While elapsed < maxWaitMs
            Await Task.Delay(100)
            elapsed += 100
            Dim result = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(checkScript)
            If result.Trim().Trim(""""c) = "true" Then
                Return True
            End If
        End While
        Return False
    End Function
    Private Async Function ClickCompleteInvoiceAsync() As Task(Of Boolean)
        Dim clickScript = "
    (function() {
        var btns = document.querySelectorAll('button.record-invoice-modal_subBox__ReN38');
        for (var i = 0; i < btns.length; i++) {
            var span = btns[i].querySelector('span');
            if (span && span.textContent.trim() === '完成开票') {
                ['mousedown', 'mouseup', 'click'].forEach(function(type) {
                    btns[i].dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                });
                return true;
            }
        }
        return false;
    })();
"
        Try
            If WebViewTaoBao.CoreWebView2 IsNot Nothing Then
                Dim result = Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(clickScript)
                If result.Trim() = "true" Then
                    Me.Invoke(Sub() lblStatus.Text = "完成开票")
                    Return True
                Else
                    Me.Invoke(Sub() lblStatus.Text = "未完成开票")
                    Return False
                End If
            End If
        Catch ex As Exception
            Me.Invoke(Sub() lblStatus.Text = "点击完成开票出错：" & ex.Message)
        End Try
        Return False
    End Function
    Private Async Function SetFileInputAsync(filePath As String,
                                          Optional selector As String = "#tsfFileName") As Task(Of Boolean)
        Try
            Await WebViewTaoBao.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.enable", "{}")

            Dim docJson As String = Await WebViewTaoBao.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "DOM.getDocument", "{""depth"":-1,""pierce"":true}")

            Dim rootNodeId As Integer = JObject.Parse(docJson)("root")("nodeId").Value(Of Integer)()

            Dim queryJson As String = $"{{""nodeId"":{rootNodeId},""selector"":""{selector}""}}"
            Dim queryResult As String = Await WebViewTaoBao.CoreWebView2.CallDevToolsProtocolMethodAsync(
    "DOM.querySelector", queryJson)

            Dim inputNodeId As Integer = JObject.Parse(queryResult)("nodeId").Value(Of Integer)()

            If inputNodeId = 0 Then
                Return False
            End If

            ' 如果 input 处于 disabled 状态，先解除禁用（保险起见）
            Dim removeDisabledJs As String = $"
            (function() {{
                var el = document.querySelector('{selector}');
                if (el) {{ el.removeAttribute('disabled'); }}
            }})();"
            Await WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(removeDisabledJs)

            Dim escapedPath As String = filePath.Replace("\", "\\")
            Dim setFilesJson As String = $"{{""files"":[""{escapedPath}""],""nodeId"":{inputNodeId}}}"
            Await WebViewTaoBao.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles", setFilesJson)

            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function
    Public Sub Openwenj()
        Dim js As String = "
        // 只点击外层按钮，把上传弹窗渲染出来（让 #tsfFileName 出现在DOM中）
        var btn = document.querySelector('.next-btn.next-large.next-btn-primary');
        if (btn) { btn.click(); }
    "
        WebViewTaoBao.CoreWebView2.ExecuteScriptAsync(js)
        lblStatus.Text = "正在准备上传..."
    End Sub
    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        ' 创建文件夹选择对话框实例
        ' 设置对话框标题
        Dim folderDialog As New FolderBrowserDialog With {
            .Description = "请选择下载文件路径"
        }

        ' 显示对话框并检查用户是否点击了确定按钮
        If folderDialog.ShowDialog() = DialogResult.OK Then
            ' 将选择的路径显示在TextBox中
            TextBox2.Text = folderDialog.SelectedPath
            config.SelectedPath = TextBox2.Text
            SaveConfig()
        End If
    End Sub
    Private Sub Global_DownloadHandler(sender As Object, e As Microsoft.Web.WebView2.Core.CoreWebView2DownloadStartingEventArgs)
        Try
            Dim targetDir As String = TextBox2.Text.Trim()
            If String.IsNullOrEmpty(targetDir) Then Return

            If Not System.IO.Directory.Exists(targetDir) Then
                System.IO.Directory.CreateDirectory(targetDir)
            End If

            ' --- 适配所有 SDK 的路径获取 ---
            ' 优先尝试 ResultFilePath 提取文件名
            Dim fileName As String = System.IO.Path.GetFileName(e.DownloadOperation.ResultFilePath)

            ' 如果文件名获取失败，则使用时间戳保底
            If String.IsNullOrEmpty(fileName) Then
                fileName = "download_" & DateTime.Now.ToString("yyyyMMddHHmmss") & ".pdf"
            End If

            ' 拼接 TextBox2 目录
            e.ResultFilePath = System.IO.Path.Combine(targetDir, fileName)

            ' 彻底隐藏另存为窗口，所有网站强制静默保存
            e.Handled = True

            Me.Invoke(Sub() lblStatus.Text = "全自动保存: " & fileName)
        Catch ex As Exception
            ' 避免弹出窗口崩溃
        End Try
    End Sub
    Private Async Sub Button11_Click(sender As Object, e As EventArgs) Handles Button11.Click
        '清除
        TextBox3.ForeColor = Color.Gray
        TextBox3.Text = "请输入订单号"
        NumericUpDown1.Value = 0

        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight

        ' 1. 点击二维码交付 -> 取URL -> 关闭弹窗
        Dim url As String = Await Xiaz()

        If String.IsNullOrEmpty(url) Then
            lblStatus.Text = "未获取到下载地址"
            Return
        End If

        ' 2. 交给隐藏的 webViewx 去下载
        Await ProcessDownloadAsync(url)
        Await Button18Core()
    End Sub
    Private Async Function Button11Core() As Task
        Await Task.Yield()
        ' 只修正了 smartClick 的寻找逻辑，其余业务流程完全保留你的原始代码
        Dim jsFlow As String = "
(function() {
    const log = (msg) => console.log('[自动化] ' + msg);

    const smartClick = (text) => {
        const elements = Array.from(document.querySelectorAll('button, span, .t-button__text, .t-select-option, div'));
        const target = elements.find(el => el.innerText.trim() === text);
        if (target) {
            const btn = target.closest('button') || target;
            ['mousedown', 'mouseup', 'click'].forEach(type => {
                btn.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
            });
            return true;
        }
        return false;
    };

    // 点击抽屉内的确定按钮（精确定位到 t-drawer__footer 内）
const clickDrawerConfirm = () => {
    const footers = document.querySelectorAll('.t-drawer__footer');
    const footer = Array.from(footers).find(f => {
        const btn = Array.from(f.querySelectorAll('button')).find(b => b.innerText.trim() === '确定');
        return btn && !btn.disabled;
    });
    if (!footer) { log('未找到可用的footer'); return false; }
    const btn = Array.from(footer.querySelectorAll('button')).find(b => b.innerText.trim() === '确定');
    let el = btn;
    for (let i = 0; i < 8; i++) {
        if (!el) break;
        const vue = el.__vue__;
        if (vue && vue.handleConfim) {
            vue.handleConfim();
            log('点击了确定');
            return true;
        }
        el = el.parentElement;
    }
    log('未找到Vue方法');
    return false;
};

    // 判断-选择票类-下拉框当前值是否为普通发票
    const isOrdinaryInvoiceSelected = () => {
        const formItem = Array.from(document.querySelectorAll('.t-form__item')).find(item => {
            const label = item.querySelector('label');
            return label && label.innerText.trim() === '选择票类';
        });
        if (!formItem) return false;
        const input = formItem.querySelector('input.t-input__inner');
        if (!input) return false;
        // TDesign select 选中值体现在 input 的 value 或 placeholder
        const val = input.value || input.placeholder || '';
        log('当前票类值: ' + val);
        return val.includes('普通发票');
    };

    // 点击-选择票类-下拉框展开
    const openInvoiceTypeSelect = () => {
        const formItem = Array.from(document.querySelectorAll('.t-form__item')).find(item => {
            const label = item.querySelector('label');
            return label && label.innerText.trim() === '选择票类';
        });
        if (!formItem) { log('未找到选择票类'); return false; }
        const input = formItem.querySelector('input.t-input__inner');
        if (input) {
            ['mousedown', 'mouseup', 'click'].forEach(type => {
                input.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
            });
            log('展开票类下拉框');
            return true;
        }
        return false;
    };

    // 在 popup 里点击普通发票选项
    const selectOrdinaryInvoice = () => {
        const options = Array.from(document.querySelectorAll('.t-popup .t-select-option'));
        const target = options.find(o => o.innerText.trim() === '普通发票');
        if (target) {
            ['mousedown', 'mouseup', 'click'].forEach(type => {
                target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
            });
            log('已选择普通发票');
            return true;
        }
        log('未找到普通发票选项');
        return false;
    };

    log('开始执行开票流程...');
    if (smartClick('继续开票')) {
        setTimeout(() => {
            if (isOrdinaryInvoiceSelected()) {
                log('已经是普通发票，直接点确定');
                clickDrawerConfirm();
            } else {
                log('需要选择普通发票，展开下拉框...');
                openInvoiceTypeSelect();

                setTimeout(() => {
                    if (selectOrdinaryInvoice()) {
                        setTimeout(() => {
                            clickDrawerConfirm();
                        }, 200);
                    } else {
                        log('选择普通发票失败');
                    }
                }, 200);
            }
        }, 500);
    }
})();"
        Try
            If WebViewTax.CoreWebView2 IsNot Nothing Then
                lblStatus.Text = "继续开票..."
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsFlow)
            End If
        Catch ex As Exception
            MessageBox.Show("执行脚本出错: " & ex.Message)
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Function

    ''' <summary>
    ''' 设置订单号文本框
    ''' </summary>
    Private Sub ConfigureTextBox()
        ' 禁用换行功能
        TextBox3.Multiline = False

        ' 设置最大长度
        TextBox3.MaxLength = 19

        ' 设置提示文字 (Placeholder)
        SetPlaceholderText()

        ' 确保没有换行符
        TextBox3.AcceptsReturn = False
        TextBox3.AcceptsTab = False

        ' 输入法限制（可选，减少中文输入干扰）
        TextBox3.ImeMode = System.Windows.Forms.ImeMode.Disable
    End Sub

    ' 设置提示文字（Placeholder效果）
    Private Sub SetPlaceholderText()
        If String.IsNullOrEmpty(TextBox3.Text) Then
            TextBox3.ForeColor = Color.Gray
            TextBox3.Text = "请输入订单号"
        End If
    End Sub

    ' 移除提示文字
    Private Sub RemovePlaceholderText()
        If TextBox3.Text = "请输入订单号" Then
            TextBox3.ForeColor = Color.Black
            TextBox3.Text = ""
        End If
    End Sub

    ' 焦点进入时清除提示文字
    Private Sub TextBox3_Enter(sender As Object, e As EventArgs) Handles TextBox3.Enter
        RemovePlaceholderText()
    End Sub

    ' 焦点离开时恢复提示文字
    Private Sub TextBox3_Leave(sender As Object, e As EventArgs) Handles TextBox3.Leave
        If String.IsNullOrEmpty(TextBox3.Text.Trim()) Then
            SetPlaceholderText()
        End If
    End Sub

    ' 键盘按下事件 - 限制输入


    ' KeyDown事件处理 - 捕获删除键（Delete）
    Private Sub TextBox3_KeyDown(sender As Object, e As KeyEventArgs) Handles TextBox3.KeyDown
        ' 阻止 Enter 键换行或提交
        If e.KeyCode = Keys.Enter OrElse e.KeyCode = Keys.Return Then
            e.SuppressKeyPress = True
        End If
        ' 不再做其他限制，让粘贴和输入都自由通过
    End Sub

    ' 文本改变事件 - 处理粘贴和非数字字符
    Private isProcessingText As Boolean = False
    Private Sub TextBox3_TextChanged(sender As Object, e As EventArgs) Handles TextBox3.TextChanged
        If isProcessingText Then Exit Sub
        If TextBox3.Text = "请输入订单号" Then
            UpdateCharacterCount(0)
            Exit Sub
        End If

        Dim tb As TextBox = DirectCast(sender, TextBox)
        Dim originalText As String = tb.Text
        Dim selectionStart As Integer = tb.SelectionStart
        Dim selectionLength As Integer = tb.SelectionLength

        ' 1. 移除非数字
        Dim cleanText As String = Regex.Replace(originalText, "[^\d]", "")

        ' 2. 限制长度
        If cleanText.Length > 19 Then
            cleanText = cleanText.Substring(0, 19)
        End If

        ' 3. 文本有变化才更新
        If cleanText <> originalText Then
            isProcessingText = True

            ' 计算光标前被删除的非数字数量
            Dim removedBefore As Integer = 0
            For i As Integer = 0 To Math.Min(selectionStart - 1, originalText.Length - 1)
                If Not Char.IsDigit(originalText(i)) Then removedBefore += 1
            Next

            ' 如果有选中区域，选中区域内的非数字也要计入删除数量
            If selectionLength > 0 Then
                For i As Integer = selectionStart To Math.Min(selectionStart + selectionLength - 1, originalText.Length - 1)
                    If Not Char.IsDigit(originalText(i)) Then removedBefore += 1
                Next
            End If

            Dim newCursorPos As Integer = selectionStart - removedBefore
            If newCursorPos < 0 Then newCursorPos = 0
            If newCursorPos > cleanText.Length Then newCursorPos = cleanText.Length

            tb.Text = cleanText
            tb.SelectionStart = newCursorPos
            tb.SelectionLength = 0

            isProcessingText = False
        End If

        ' 4. 更新字符计数
        UpdateCharacterCount(cleanText.Length)
    End Sub

    ' 实时显示字符数（可选功能）
    Private Sub UpdateCharacterCount(currentLength As Integer)
        ' 可以在这里添加Label控件来显示字符数
        ' 例如：Label1.Text = $"当前字符数: {currentLength}/19"

        ' 如果添加了Label1控件，取消下面一行的注释
        ' Label1.Text = $"当前字符数: {currentLength}/19"

        ' 改变颜色提示（当长度不为19时）
        If currentLength > 0 AndAlso currentLength < 19 Then
            ' 可以在这里设置文本框或标签的颜色来提醒用户
            TextBox3.ForeColor = Color.Red
            ' 但我们选择在验证时提示，所以这里不改变颜色
        End If
    End Sub

    ' 验证事件 - 最终验证
    Private Sub TextBox3_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles TextBox3.Validating
        Dim textBox As TextBox = DirectCast(sender, TextBox)

        ' 如果不是提示文字且不为空，进行验证
        If textBox.Text <> "请输入订单号" AndAlso Not String.IsNullOrEmpty(textBox.Text) Then
            ' 确保只有数字
            If Not IsNumeric(textBox.Text) Then
                MessageBox.Show("订单号只能包含数字！", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                textBox.Focus()
                textBox.SelectAll()
                e.Cancel = True
                Return
            End If

            ' 验证长度 - 必须为19位
            If textBox.Text.Length <> 19 Then
                Dim result As DialogResult = MessageBox.Show(
                    $"订单号必须为19位数字，当前为{textBox.Text.Length}位。" & vbCrLf &
                    "是否继续编辑？" & vbCrLf &
                    "点击【是】继续编辑，点击【否】清空内容重新输入。",
                    "订单号长度错误",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning)

                If result = DialogResult.Yes Then
                    ' 用户选择继续编辑，保持焦点
                    textBox.Focus()
                    textBox.SelectAll()
                    e.Cancel = True
                Else
                    ' 用户选择清空内容
                    textBox.Text = ""
                    SetPlaceholderText()
                    e.Cancel = False
                End If
                Return
            End If

            ' 验证通过，可以在这里执行其他验证逻辑
            If Not ValidateOrderNumber(textBox.Text) Then
                MessageBox.Show("订单号格式不正确！", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                textBox.Focus()
                textBox.SelectAll()
                e.Cancel = True
            End If
        End If
    End Sub

    ' 自定义订单号验证逻辑（可选）
    Private Function ValidateOrderNumber(orderNumber As String) As Boolean
        ' 这里可以添加更复杂的验证逻辑
        ' 例如：检查订单号是否有效，是否符合特定规则等

        ' 示例：订单号必须以特定前缀开头
        ' If Not orderNumber.StartsWith("2023") Then
        '     Return False
        ' End If

        ' 示例：订单号必须符合校验规则（Luhn算法）
        ' If Not CheckLuhn(orderNumber) Then
        '     Return False
        ' End If

        Return True
    End Function

    ' Luhn算法验证（示例，可选）
    Private Function CheckLuhn(number As String) As Boolean
        Dim sum As Integer = 0
        Dim alternate As Boolean = False

        For i As Integer = number.Length - 1 To 0 Step -1
            Dim digit As Integer = CInt(number(i).ToString())

            If alternate Then
                digit *= 2
                If digit > 9 Then
                    digit = digit \ 10 + digit Mod 10
                End If
            End If

            sum += digit
            alternate = Not alternate
        Next

        Return sum Mod 10 = 0
    End Function

    ' 粘贴事件处理（可选，增强粘贴控制）
    Private Sub TextBox3_Paste(sender As Object, e As EventArgs) Handles TextBox3.Click
        ' 这里可以添加自定义粘贴处理逻辑
        ' 但TextChanged事件已经处理了非数字字符，所以这里不需要额外处理
    End Sub

    Private Sub Button25_Click(sender As Object, e As EventArgs) Handles Button25.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        If txtInvoiceInfo.Lines.Length > 0 Then
            Dim secondLine As String = txtInvoiceInfo.Lines(0).Replace("订单号:", "").Replace("订单号：", "").Trim()
            Clipboard.SetText(secondLine)
        Else
            MessageBox.Show("数据内容为空。")
        End If
    End Sub
    Private Sub Button12_Click(sender As Object, e As EventArgs) Handles Button12.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        If TextBox1.Lines.Length > 1 Then
            Dim secondLine As String = TextBox1.Lines(1)
            Clipboard.SetText(secondLine)
        Else
            MessageBox.Show("数据内容为空。")
        End If
    End Sub
    Private Sub Button13_Click(sender As Object, e As EventArgs) Handles Button13.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        If TextBox1.Lines.Length > 2 Then
            Dim secondLine As String = TextBox1.Lines(2)
            Clipboard.SetText(secondLine)
        Else
            MessageBox.Show("数据内容为空。")
        End If
    End Sub

    Private Sub Button14_Click(sender As Object, e As EventArgs) Handles Button14.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        If TextBox1.Lines.Length > 3 Then
            Dim secondLine As String = TextBox1.Lines(3)
            Clipboard.SetText(secondLine)
        Else
            MessageBox.Show("数据内容为空。")
        End If
    End Sub

    Private Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Button1.Enabled = False
        Me.ActiveControl = Nothing
        Me.Focus()
        Try
            For Each line In projectNamex.Text.Split({vbCrLf}, StringSplitOptions.RemoveEmptyEntries)
                If line.StartsWith("邮箱：") Then
                    Dim value = line.Substring(3).Trim()
                    If Not String.IsNullOrWhiteSpace(value) Then
                        If Await SetClipboardTextAsync(value) Then
                            lblStatus.Text = "已复制：" & value
                        Else
                            lblStatus.Text = "复制失败，请稍后重试。"
                        End If
                    End If
                End If
            Next
        Finally
            Button1.Enabled = True
        End Try
    End Sub

    Private Async Function SetClipboardTextAsync(text As String,
                                         Optional maxRetries As Integer = 10,
                                         Optional delayMs As Integer = 50) As Task(Of Boolean)
        For i As Integer = 0 To maxRetries - 1
            Try
                System.Windows.Clipboard.SetDataObject(text, False)
                Return True
            Catch ex As System.Runtime.InteropServices.COMException
                If ex.HResult <> &H800401D0 Then Throw
            End Try

            If i < maxRetries - 1 Then
                Await Task.Delay(delayMs)
            End If
        Next
        Return False
    End Function
    '组合按键
    Private Async Sub Button15_Click(sender As Object, e As EventArgs) Handles Button15.Click
        TextBox3.ForeColor = Color.Gray
        TextBox3.Text = "请输入订单号"
        NumericUpDown1.Value = 0
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight

        Dim url As String = Await Xiaz()
        If String.IsNullOrEmpty(url) Then
            lblStatus.Text = "未获取到下载地址"
            Return
        End If

        ' 关键：并行启动，不等下载完成就开始开票
        Dim tDownload = ProcessDownloadAsync(url)
        Dim tBtn11 = Button11Core()

        Dim newPdf = Await WaitForDownloadAsync(10000)

        If newPdf Is Nothing Then
            lblStatus.Text = "超时或下载失败，请手动操作"
            Await Task.WhenAll(tDownload, tBtn11)
            Return
        End If

        Dim tBtn9 = Button9Core(newPdf)
        Await Task.WhenAll(tDownload, tBtn11, tBtn9)

        Me.ActiveControl = Nothing
        Me.Focus()
    End Sub
    Private Async Function WaitForDownloadAsync(timeoutMs As Integer) As Task(Of String)
        If downloadTcs Is Nothing Then Return Nothing
        lblStatus.Text = "正在下载PDF，请稍候..."
        Dim completed = Await Task.WhenAny(downloadTcs.Task, Task.Delay(timeoutMs))
        If completed Is downloadTcs.Task Then
            Dim result = Await downloadTcs.Task
            If result IsNot Nothing Then
                lblStatus.Text = "PDF下载完成"
            End If
            Return result
        End If
        lblStatus.Text = "下载超时，请手动操作"
        Return Nothing
    End Function
    Private Function Button18Core() As Task
        Me.ActiveControl = Nothing
        Me.Focus()
        Dim folderPath As String = TextBox2.Text.Trim()

        ' 检查路径是否为空
        If String.IsNullOrWhiteSpace(folderPath) Then
            MessageBox.Show("请先在 TextBox2 中输入或选择文件夹路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return Task.CompletedTask
        End If

        ' 检查文件夹是否存在
        If Not System.IO.Directory.Exists(folderPath) Then
            MessageBox.Show($"文件夹不存在：{folderPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return Task.CompletedTask
        End If

        Try
            ' 打开文件夹（资源管理器）
            Process.Start("explorer.exe", folderPath)
        Catch ex As Exception
            MessageBox.Show($"打开文件夹失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        Return Task.CompletedTask
    End Function
    Private Sub Button18_Click(sender As Object, e As EventArgs) Handles Button18.Click
        Button18Core()
    End Sub

    Private Async Sub Button16_Click(sender As Object, e As EventArgs) Handles Button16.Click
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        Me.ActiveControl = Nothing
        Me.Focus()
        Dim homeUrl As String = TaxSiteHelper.BuildTaxUrl(config.Region)
        Dim closeScript As String =
        $"(function() {{ window.location.href = '{homeUrl}'; }})();"

        Await WebViewTax.CoreWebView2.ExecuteScriptAsync(closeScript)
        lblStatus.Text = "已返回首页（" & config.Region & "）。"
    End Sub
    Private Async Function Butt_yz() As Task(Of Boolean)
        Dim rowCount As Integer = CInt(NumericUpDown1.Value)
        If rowCount <> 0 Then Return True

        Dim Purchasenumber As String = ExtractValue(projectNamex.Text, "购方税号")
        If String.IsNullOrEmpty(Purchasenumber) Then
            lblStatus.Text = "税号为空不匹配"
            Return False
        End If

        lblStatus.Text = "匹配税号正确中..."
        lblStatus.ForeColor = System.Drawing.SystemColors.MenuHighlight
        '验证税号
        Dim jsGmf As String = <![CDATA[
window.__gmfResult = null;
var container = document.querySelector('.t-form-item__gmfnsrsbh');
if (container) {
    var input = container.querySelector('input');
    if (input) {
        input.focus();
        input.click();
    }
}
setTimeout(function() {
    var item = document.querySelector('.auto-complete__item');
    if (item) {
        item.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
    }
    setTimeout(function() {
        document.body.click();
        setTimeout(function() {
            var container2 = document.querySelector('.t-form-item__gmfnsrsbh');
            if (container2) {
                var input2 = container2.querySelector('input');
                if (input2) {
                    input2.focus();
                    input2.click();
                }
            }
            setTimeout(function() {
                var item2 = document.querySelector('.auto-complete__item');
                if (!item2) { window.__gmfResult = '匹配失败'; return; }
                item2.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
                window.__gmfResult = '匹配正确';
            }, 1500);
        }, 300);
    }, 300);
}, 1500);
]]>.Value
        WebViewTax.Focus()
        Await Task.Delay(200)
        Await WebViewTax.CoreWebView2.ExecuteScriptAsync(jsGmf)

        Dim result As String = "null"
        For i As Integer = 1 To 40
            Await Task.Delay(100)
            result = Await WebViewTax.CoreWebView2.ExecuteScriptAsync("window.__gmfResult;")
            If result <> "null" Then Exit For
        Next

        lblStatus.Text = result.Trim(""""c)
        Dim success As Boolean = (lblStatus.Text = "匹配正确")
        lblStatus.ForeColor = If(success, Color.LimeGreen, Color.Crimson)
        Return success
    End Function

    Private Sub BtnManageConditions_Click(sender As Object, e As EventArgs) Handles btnManageConditions.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        ' 打开弹窗，传入当前 config
        Using frm As New FormConditionList(Me.config)
            Dim result = frm.ShowDialog()
            ' 弹窗关闭后，由于 config.ConditionList 已经被直接修改，所以只需保存配置文件
            If result = DialogResult.OK Then
                SaveConfig()   ' 你已有的保存方法
                ' 可选：刷新主窗体上绑定 ConditionList 的控件（如果有）
                'RefreshConditionListDisplay()
            End If
        End Using
    End Sub
    Private Async Function Button23Core() As Task
        Try
            Dim bank As String = ""
            Dim bankAccount As String = ""

            Dim script As String = "(function() {
        function fillSingleField(selector, value) {
            const container = document.querySelector(selector);
            if (!container) return false;
            const input = container.querySelector('input');
            if (!input) return false;

            input.removeAttribute('readonly');
            input.removeAttribute('disabled');
            input.disabled = false;
            input.readOnly = false;
            input.focus();
            input.click();

            const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            nativeSetter.call(input, value);

            const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
            for (let evtType of events) {
                const event = new Event(evtType, { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'target', {writable: false, value: input});
                input.dispatchEvent(event);
            }
            try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            return true;
        }

        const r1 = fillSingleField('.t-form-item__xsfkhh', '" & bank & "');
        const r2 = fillSingleField('.t-form-item__xsfyhzh', '" & bankAccount & "');
        return JSON.stringify({ bank: r1, account: r2 });
    })();"

            If WebViewTax.CoreWebView2 IsNot Nothing Then
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            MessageBox.Show("异常: " & ex.Message)
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Function
    Private Async Function Button24Core() As Task
        Try
            Dim bank As String = ""
            Dim bankAccount As String = ""

            Dim script As String = "(function() {
        function fillSingleField(selector, value) {
            const container = document.querySelector(selector);
            if (!container) return false;
            const input = container.querySelector('input');
            if (!input) return false;

            input.removeAttribute('readonly');
            input.removeAttribute('disabled');
            input.disabled = false;
            input.readOnly = false;
            input.focus();
            input.click();

            const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            nativeSetter.call(input, value);

            const events = ['focus', 'keydown', 'keypress', 'input', 'keyup', 'change', 'blur'];
            for (let evtType of events) {
                const event = new Event(evtType, { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'target', {writable: false, value: input});
                input.dispatchEvent(event);
            }
            try { input.select(); document.execCommand('insertText', false, value); } catch (e) {}
            return true;
        }

        const r1 = fillSingleField('.t-form-item__xsfkhh', '" & bank & "');
        const r2 = fillSingleField('.t-form-item__xsfyhzh', '" & bankAccount & "');
        return JSON.stringify({ bank: r1, account: r2 });
    })();"

            If WebViewTax.CoreWebView2 IsNot Nothing Then
                Await WebViewTax.CoreWebView2.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            MessageBox.Show("异常: " & ex.Message)
        End Try
        Me.ActiveControl = Nothing
        Me.Focus()
    End Function

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        config.SaveDrawer = CheckBox1.Checked
    End Sub
    ''' <summary>Button23：切换 淘宝/天猫 ↔ 1688 模式</summary>
    Private Sub Button23_Click(sender As Object, e As EventArgs) Handles Button23.Click
        Me.ActiveControl = Nothing
        Me.Focus()

        isB2BMode = Not isB2BMode
        config.IsB2BMode = isB2BMode
        SaveConfig()

        If isB2BMode Then
            shangjiang = "2"
            lblStatus.Text = "已切换到1688模式，请登录1688"
        Else
            shangjiang = "1"   ' 切回后交给自动识别修正（淘宝/天猫）
            lblStatus.Text = "已切换回淘宝/天猫模式"
        End If

        LoadTaoBaoWebPage()
        UpdateB2BButton()
        '清除
        TextBox3.ForeColor = Color.Gray
        TextBox3.Text = "请输入订单号"
        projectNamex.Text = ""
    End Sub

    ''' <summary>更新按钮文字显示当前模式</summary>
    Private Sub UpdateB2BButton()
        If isB2BMode Then
            Button23.Text = "当前：1688（点切换淘宝/天猫）"
        Else
            Button23.Text = "当前：淘宝/天猫（点切换1688）"
        End If
    End Sub

    Private Sub Button24_Click(sender As Object, e As EventArgs) Handles Button24.Click
        Me.ActiveControl = Nothing
        Me.Focus()
        Using f As New Form4(config)
            If f.ShowDialog(Me) = DialogResult.OK Then
                ' 保存成功后可在这里刷新界面
                ' RefreshUiFromConfig()
            End If
        End Using
    End Sub
End Class