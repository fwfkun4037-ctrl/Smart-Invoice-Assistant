Imports System.Collections.Generic
Module TaxSiteHelper
    Private Const UrlTemplate As String = "https://etax.{0}.chinatax.gov.cn:8443/"

    ' 用 List 保序，ComboBox 顺序永远固定
    Public ReadOnly RegionCodes As New List(Of KeyValuePair(Of String, String)) From {
        New KeyValuePair(Of String, String)("北京市", "beijing"),
        New KeyValuePair(Of String, String)("安徽省", "anhui"),
        New KeyValuePair(Of String, String)("福建省", "fujian"),
        New KeyValuePair(Of String, String)("广东省", "guangdong"),
        New KeyValuePair(Of String, String)("广西壮族自治区", "guangxi"),
        New KeyValuePair(Of String, String)("重庆市", "chongqing"),
        New KeyValuePair(Of String, String)("贵州省", "guizhou"),
        New KeyValuePair(Of String, String)("甘肃省", "gansu"),
        New KeyValuePair(Of String, String)("大连市", "dalian"),
        New KeyValuePair(Of String, String)("河北省", "hebei"),
        New KeyValuePair(Of String, String)("吉林省", "jilin"),
        New KeyValuePair(Of String, String)("黑龙江省", "heilongjiang"),
        New KeyValuePair(Of String, String)("江苏省", "jiangsu"),
        New KeyValuePair(Of String, String)("江西省", "jiangxi"),
        New KeyValuePair(Of String, String)("河南省", "henan"),
        New KeyValuePair(Of String, String)("湖北省", "hubei"),
        New KeyValuePair(Of String, String)("湖南省", "hunan"),
        New KeyValuePair(Of String, String)("海南省", "hainan"),
        New KeyValuePair(Of String, String)("山西省", "shanxi"),
        New KeyValuePair(Of String, String)("内蒙古自治区", "neimenggu"),
        New KeyValuePair(Of String, String)("辽宁省", "liaoning"),
        New KeyValuePair(Of String, String)("上海市", "shanghai"),
        New KeyValuePair(Of String, String)("山东省", "shandong"),
        New KeyValuePair(Of String, String)("四川省", "sichuan"),
        New KeyValuePair(Of String, String)("陕西省", "shaanxi"),
        New KeyValuePair(Of String, String)("青海省", "qinghai"),
        New KeyValuePair(Of String, String)("宁夏回族自治区", "ningxia"),
        New KeyValuePair(Of String, String)("宁波市", "ningbo"),
        New KeyValuePair(Of String, String)("青岛市", "qingdao"),
        New KeyValuePair(Of String, String)("深圳市", "shenzhen"),
        New KeyValuePair(Of String, String)("天津市", "tianjin"),
        New KeyValuePair(Of String, String)("浙江省", "zhejiang"),
        New KeyValuePair(Of String, String)("云南省", "yunnan"),
        New KeyValuePair(Of String, String)("西藏自治区", "xizang"),
        New KeyValuePair(Of String, String)("新疆维吾尔自治区", "xinjiang"),
        New KeyValuePair(Of String, String)("厦门市", "xiamen")
    }

    ' 中文名列表（给 ComboBox 用）
    Public Function GetRegionNames() As List(Of String)
        Dim names As New List(Of String)
        For Each kv In RegionCodes
            names.Add(kv.Key)
        Next
        Return names
    End Function

    ' 按中文名拼 URL，找不到返回 Nothing
    Public Function BuildTaxUrl(regionName As String) As String
        For Each kv In RegionCodes
            If kv.Key = regionName Then
                Return String.Format(UrlTemplate, kv.Value)
            End If
        Next
        Return Nothing
    End Function
End Module
