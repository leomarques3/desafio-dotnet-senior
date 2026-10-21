Imports System
Imports System.Text

Public Class ValidadorDocumento

    Public Shared Function SomenteDigitos(ByVal doc As String) As String
        If doc Is Nothing Then Return ""
        Dim sb As New StringBuilder()
        For Each c As Char In doc
            If Char.IsDigit(c) Then sb.Append(c)
        Next
        Return sb.ToString()
    End Function

    Public Shared Function Normalizar(ByVal doc As String) As String
        Dim d As String = SemMascara(doc)
        If CnpjAlfanumerico(d) Then Return d
        Return SomenteDigitos(doc)
    End Function

    Public Shared Function EhAlfanumerico(ByVal doc As String) As Boolean
        If doc Is Nothing Then Return False
        For Each c As Char In doc
            If Char.IsLetter(c) Then Return True
        Next
        Return False
    End Function

    Public Shared Function ParaNumero(ByVal doc As String) As Long
        Dim d As String = SomenteDigitos(doc)
        If d = "" Then Return 0
        Return CLng(d)
    End Function

    Public Shared Function ValidarCpf(ByVal doc As String) As Boolean
        Dim d As String = SomenteDigitos(doc)
        If d.Length <> 11 Then Return False
        If Not IsNumeric(d) Then Return False
        If TodosIguais(d) Then Return False

        Dim soma As Integer = 0
        For i As Integer = 0 To 8
            soma += CInt(d.Substring(i, 1)) * (10 - i)
        Next
        Dim dv1 As Integer = (soma * 10) Mod 11
        If dv1 = 10 Then dv1 = 0
        If dv1 <> CInt(d.Substring(9, 1)) Then Return False

        soma = 0
        For i As Integer = 0 To 9
            soma += CInt(d.Substring(i, 1)) * (11 - i)
        Next
        Dim dv2 As Integer = (soma * 10) Mod 11
        If dv2 = 10 Then dv2 = 0
        Return dv2 = CInt(d.Substring(10, 1))
    End Function

    Public Shared Function ValidarCnpj(ByVal doc As String) As Boolean
        If doc Is Nothing Then Return False
        Dim d As String = doc.Trim().Replace(".", "").Replace("/", "").Replace("-", "")
        If d.Length <> 14 Then Return False
        If Not IsNumeric(d) Then Return CnpjAlfanumerico(d.ToUpperInvariant())
        If CLng(d) = 0 Then Return False
        If TodosIguais(d) Then Return False

        Dim pesos1() As Integer = {5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2}
        Dim pesos2() As Integer = {6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2}

        Dim soma As Integer = 0
        For i As Integer = 0 To 11
            soma += CInt(d.Substring(i, 1)) * pesos1(i)
        Next
        Dim resto As Integer = soma Mod 11
        Dim dv1 As Integer = If(resto < 2, 0, 11 - resto)
        If dv1 <> CInt(d.Substring(12, 1)) Then Return False

        soma = 0
        For i As Integer = 0 To 12
            soma += CInt(d.Substring(i, 1)) * pesos2(i)
        Next
        resto = soma Mod 11
        Dim dv2 As Integer = If(resto < 2, 0, 11 - resto)
        Return dv2 = CInt(d.Substring(13, 1))
    End Function

    Public Shared Function ValidarCpfOuCnpj(ByVal doc As String) As Boolean
        If CnpjAlfanumerico(SemMascara(doc)) Then Return True
        Dim d As String = SomenteDigitos(doc)
        If d.Length = 11 Then Return ValidarCpf(d)
        If d.Length = 14 Then Return ValidarCnpj(d)
        Return False
    End Function

    Private Shared Function SemMascara(ByVal doc As String) As String
        If doc Is Nothing Then Return ""
        Return doc.Trim().Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "").ToUpperInvariant()
    End Function

    Private Shared Function CnpjAlfanumerico(ByVal d As String) As Boolean
        If d.Length <> 14 Then Return False
        Dim temLetra As Boolean = False
        For i As Integer = 0 To 13
            Dim c As Char = d(i)
            If c >= "A"c AndAlso c <= "Z"c AndAlso i < 12 Then
                temLetra = True
            ElseIf c < "0"c OrElse c > "9"c Then
                Return False
            End If
        Next
        If Not temLetra Then Return False
        Return DigitoCnpj(d, 12) = AscW(d(12)) - 48 AndAlso DigitoCnpj(d, 13) = AscW(d(13)) - 48
    End Function

    Private Shared Function DigitoCnpj(ByVal d As String, ByVal tamanho As Integer) As Integer
        Dim soma As Integer = 0
        Dim peso As Integer = 2
        For i As Integer = tamanho - 1 To 0 Step -1
            soma += (AscW(d(i)) - 48) * peso
            peso = If(peso = 9, 2, peso + 1)
        Next
        Dim resto As Integer = soma Mod 11
        Return If(resto < 2, 0, 11 - resto)
    End Function

    Private Shared Function TodosIguais(ByVal d As String) As Boolean
        For i As Integer = 1 To d.Length - 1
            If d(i) <> d(0) Then Return False
        Next
        Return True
    End Function

End Class
