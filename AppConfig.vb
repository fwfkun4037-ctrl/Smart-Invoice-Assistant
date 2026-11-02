Imports Newtonsoft.Json
Imports System.Security.Cryptography
Imports System.Text
Imports System.Collections.Generic

Public Class AppConfig
    ' ========== 加密存储的字段（JSON 序列化这些） ==========
    <JsonProperty> Public Property EncryptedTaxNumber1 As String = ""
    <JsonProperty> Public Property EncryptedPhone1 As String = ""
    <JsonProperty> Public Property EncryptedPasswordx1 As String = ""

    <JsonProperty> Public Property EncryptedTaxNumber2 As String = ""
    <JsonProperty> Public Property EncryptedPhone2 As String = ""
    <JsonProperty> Public Property EncryptedPasswordx2 As String = ""

    <JsonProperty> Public Property EncryptedTaxNumber3 As String = ""
    <JsonProperty> Public Property EncryptedPhone3 As String = ""
    <JsonProperty> Public Property EncryptedPasswordx3 As String = ""

    <JsonProperty> Public Property EncryptedTaxNumber4 As String = ""
    <JsonProperty> Public Property EncryptedPhone4 As String = ""
    <JsonProperty> Public Property EncryptedPasswordx4 As String = ""
    <JsonProperty> Public Property EncryptedTaxNumber5 As String = ""
    <JsonProperty> Public Property EncryptedPhone5 As String = ""
    <JsonProperty> Public Property EncryptedPasswordx5 As String = ""
    <JsonProperty> Public Property EncryptedAccount1 As String = ""
    <JsonProperty> Public Property EncryptedPassword1 As String = ""

    <JsonProperty> Public Property EncryptedAccount2 As String = ""
    <JsonProperty> Public Property EncryptedPassword2 As String = ""
    <JsonProperty> Public Property EncryptedAccount3 As String = ""
    <JsonProperty> Public Property EncryptedPassword3 As String = ""

    <JsonProperty> Public Property EncryptedAccount4 As String = ""
    <JsonProperty> Public Property EncryptedPassword4 As String = ""
    <JsonProperty> Public Property EncryptedAccount5 As String = ""
    <JsonProperty> Public Property EncryptedPassword5 As String = ""

    <JsonProperty> Public Property EncryptedTaoBaoCookies As String = ""
    <JsonProperty> Public Property EncryptedTaxCookies As String = ""
    <JsonProperty> Public Property EncryptedB2BCookies As String = ""

    ' ========== 非敏感属性，直接存储 ==========
    Public Property SavePassword As Boolean = True
    Public Property SaveDrawer As Boolean = True
    Public Property CurrentTaxAccountIndex As Integer = 0   ' 0:账号1, 1:账号2
    Public Property CurrentAccountIndex As Integer = 0
    Public Property IsB2BMode As Boolean = False
    Public Property SelectedPath As String = ""
    ' ⭐ 新增：用户自定义规格列表（所有需要触发“件”的规格名称）
    <JsonProperty>   ' 可加也可不加，默认会序列化公开属性
    Public Property TriggerUnit As String = "件"
    Public Property ConditionList As List(Of String) = New List(Of String) From {}

    ' ⭐ Form4：登录区域 + 智谱AI Key
    Public Property Region As String = "浙江省"
    Public Property ZhipuApiKey As String = ""

    ' ⭐ Form4：4 组公司 / 店铺
    Public Property Company1 As String = ""
    Public Property Shop1 As String = ""
    Public Property Company2 As String = ""
    Public Property Shop2 As String = ""
    Public Property Company3 As String = ""
    Public Property Shop3 As String = ""
    Public Property Company4 As String = ""
    Public Property Shop4 As String = ""

    ' ⭐ Form4：开票规格
    Public Property DefaultSpecOption As String = ""
    Public Property Keywords1 As String = ""
    Public Property Keywords2 As String = ""
    Public Property Keywords3 As String = ""
    Public Property Keywords4 As String = ""
    Public Property Keywords5 As String = ""
    Public Property Keywords6 As String = ""
    Public Property Keywords7 As String = ""
    Public Property Spec1 As String = ""
    Public Property Spec2 As String = ""
    Public Property Spec3 As String = ""
    Public Property Spec4 As String = ""
    Public Property Spec5 As String = ""
    Public Property Spec6 As String = ""
    Public Property Spec7 As String = ""

    ' ⭐ Form4：开票人
    Public Property Payee As String = ""
    Public Property Reviewer As String = ""

    ' ========== 明文访问属性（自动加解密） ==========
    <JsonIgnore> Public Property TaxNumber1 As String
        Get
            Return Decrypt(EncryptedTaxNumber1)
        End Get
        Set(value As String)
            EncryptedTaxNumber1 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Phone1 As String
        Get
            Return Decrypt(EncryptedPhone1)
        End Get
        Set(value As String)
            EncryptedPhone1 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Passwordx1 As String
        Get
            Return Decrypt(EncryptedPasswordx1)
        End Get
        Set(value As String)
            EncryptedPasswordx1 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property TaxNumber2 As String
        Get
            Return Decrypt(EncryptedTaxNumber2)
        End Get
        Set(value As String)
            EncryptedTaxNumber2 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Phone2 As String
        Get
            Return Decrypt(EncryptedPhone2)
        End Get
        Set(value As String)
            EncryptedPhone2 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Passwordx2 As String
        Get
            Return Decrypt(EncryptedPasswordx2)
        End Get
        Set(value As String)
            EncryptedPasswordx2 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property TaxNumber3 As String
        Get
            Return Decrypt(EncryptedTaxNumber3)
        End Get
        Set(value As String)
            EncryptedTaxNumber3 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Phone3 As String
        Get
            Return Decrypt(EncryptedPhone3)
        End Get
        Set(value As String)
            EncryptedPhone3 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Passwordx3 As String
        Get
            Return Decrypt(EncryptedPasswordx3)
        End Get
        Set(value As String)
            EncryptedPasswordx3 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property TaxNumber4 As String
        Get
            Return Decrypt(EncryptedTaxNumber4)
        End Get
        Set(value As String)
            EncryptedTaxNumber4 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Phone4 As String
        Get
            Return Decrypt(EncryptedPhone4)
        End Get
        Set(value As String)
            EncryptedPhone4 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Passwordx4 As String
        Get
            Return Decrypt(EncryptedPasswordx4)
        End Get
        Set(value As String)
            EncryptedPasswordx4 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property TaxNumber5 As String
        Get
            Return Decrypt(EncryptedTaxNumber5)
        End Get
        Set(value As String)
            EncryptedTaxNumber5 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Phone5 As String
        Get
            Return Decrypt(EncryptedPhone5)
        End Get
        Set(value As String)
            EncryptedPhone5 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Passwordx5 As String
        Get
            Return Decrypt(EncryptedPasswordx5)
        End Get
        Set(value As String)
            EncryptedPasswordx5 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property Account1 As String
        Get
            Return Decrypt(EncryptedAccount1)
        End Get
        Set(value As String)
            EncryptedAccount1 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Password1 As String
        Get
            Return Decrypt(EncryptedPassword1)
        End Get
        Set(value As String)
            EncryptedPassword1 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Account2 As String
        Get
            Return Decrypt(EncryptedAccount2)
        End Get
        Set(value As String)
            EncryptedAccount2 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Password2 As String
        Get
            Return Decrypt(EncryptedPassword2)
        End Get
        Set(value As String)
            EncryptedPassword2 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property Account3 As String
        Get
            Return Decrypt(EncryptedAccount3)
        End Get
        Set(value As String)
            EncryptedAccount3 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Password3 As String
        Get
            Return Decrypt(EncryptedPassword3)
        End Get
        Set(value As String)
            EncryptedPassword3 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property Account4 As String
        Get
            Return Decrypt(EncryptedAccount4)
        End Get
        Set(value As String)
            EncryptedAccount4 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Password4 As String
        Get
            Return Decrypt(EncryptedPassword4)
        End Get
        Set(value As String)
            EncryptedPassword4 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property Account5 As String
        Get
            Return Decrypt(EncryptedAccount5)
        End Get
        Set(value As String)
            EncryptedAccount5 = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property Password5 As String
        Get
            Return Decrypt(EncryptedPassword5)
        End Get
        Set(value As String)
            EncryptedPassword5 = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property TaoBaoCookies As String
        Get
            Return Decrypt(EncryptedTaoBaoCookies)
        End Get
        Set(value As String)
            EncryptedTaoBaoCookies = Encrypt(value)
        End Set
    End Property

    <JsonIgnore> Public Property TaxCookies As String
        Get
            Return Decrypt(EncryptedTaxCookies)
        End Get
        Set(value As String)
            EncryptedTaxCookies = Encrypt(value)
        End Set
    End Property
    <JsonIgnore> Public Property B2BCookies As String
        Get
            Return Decrypt(EncryptedB2BCookies)
        End Get
        Set(value As String)
            EncryptedB2BCookies = Encrypt(value)
        End Set
    End Property


    ' ========== 加密解密方法（DPAPI，当前用户上下文） ==========
    Private Function Encrypt(plainText As String) As String
        If String.IsNullOrEmpty(plainText) Then Return ""
        Dim plainBytes As Byte() = Encoding.UTF8.GetBytes(plainText)
        Dim encryptedBytes As Byte() = ProtectedData.Protect(plainBytes, Nothing, DataProtectionScope.CurrentUser)
        Return Convert.ToBase64String(encryptedBytes)
    End Function

    Private Function Decrypt(cipherBase64 As String) As String
        If String.IsNullOrEmpty(cipherBase64) Then Return ""
        Dim encryptedBytes As Byte() = Convert.FromBase64String(cipherBase64)
        Dim plainBytes As Byte() = ProtectedData.Unprotect(encryptedBytes, Nothing, DataProtectionScope.CurrentUser)
        Return Encoding.UTF8.GetString(plainBytes)
    End Function
End Class