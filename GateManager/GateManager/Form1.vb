Public Class Form1
    Private ws As WebReference.KanriService
    Private destR As WebReference.RoomsInfo
    'Private wss As WebReference.PersonInfo
    Private jpnPurpose() As String
    Private engPurpose() As String

    Private idCol As Collection
    Private MyRoomID As Integer

    Private FLock As Boolean

    Private EnterdPersons As Collection '入室済みの人のIDを保存

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        Me.FormBorderStyle = Windows.Forms.FormBorderStyle.Fixed3D

        idCol = New Collection
        EnterdPersons = New Collection

        ReadMyRooms()

        ws = New WebReference.KanriService

        'PLCと接続
        Me.AxEng1.StartUpdate()

        Me.Timer1.Enabled = True
    End Sub

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox1.TextChanged
        Dim tmpBx As TextBox = CType(sender, TextBox)
        Dim a As String = tmpBx.Text
        Dim pno As String
        Dim sno As String
        If InStr(a, vbCrLf) Then
            '読み取り器の番号を判定
            Dim b As String = Microsoft.VisualBasic.Left(a, 1)
            If b = "B" Then
                sno = "2"
            Else
                sno = "1"
            End If
            'フィルムバッチかどうかを判定
            Dim l As String = Microsoft.VisualBasic.Mid(a, 2, 1)
            If l Like "[0-9]" Then
                pno = "0" & Microsoft.VisualBasic.Mid(a, 2, 5)
            Else
                Dim j As Integer = Asc(l) - &H40

                pno = Format(j, "0") & Microsoft.VisualBasic.Mid(a, 3, 5)
            End If

            'Me.MessageQueue1.Send(sno & pno)
            Me.TextBox2.Text = sno & pno
            idCol.Add(sno & pno)
            tmpBx.Text = ""

        End If
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Dim f As Form = Form.ActiveForm
        If f Is Nothing Then
            'Me.TextBox2.Text = "Not Active!!"
            Me.WindowState = FormWindowState.Minimized
            Me.WindowState = FormWindowState.Normal
            Me.TextBox1.Focus()
            'Else
            'Me.TextBox2.Text = "Now Active!!"
        End If
        EntryPerson()
    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
        Me.Dispose()
    End Sub

    Private Sub EntryPerson()
        If idCol.Count = 0 Then Exit Sub
        Dim strCardID As String = idCol(1)
        idCol.Remove(1)
        Dim CardID As Integer = Val(Microsoft.VisualBasic.Right(strCardID, 6))
        Dim direction As Integer = Val(Microsoft.VisualBasic.Left(strCardID, 1))
        Me.Label1.Text = ""
        Dim wss1 As WebReference.PersonInfo = ws.GetPersonInfo(CardID)
        If wss1.PersonName = "" Then
            Me.Label1.Text = "ID(" & CardID.ToString & ")が見つかりません"
            Exit Sub
        End If
        '入室か退室かをチェック
        If direction = 1 Then '入室の場合
             '既に入室状態となっているかどうかをチェック
            If ws.ExistRoom(CardID, MyRoomID) Then
                Dim msg As String
                If wss1.LangType = "Jpn" Then
                    msg = wss1.PersonName & "さんは既に入室状態です。"
                Else
                    msg = wss1.PersonName & " is already exist."
                End If
                Me.Label1.Text = msg

                '入室が可能かどうかをチェックする
            ElseIf Not ws.IsThisAllow(CardID, MyRoomID) Then
                Dim msg As String
                If wss1.LangType = "Jpn" Then
                    msg = wss1.PersonName & "さんは入室を許可されていません。"
                Else
                    msg = wss1.PersonName & " is not allowed to enter this room."
                End If
                Me.Label1.Text = msg
                Exit Sub
            Else
                ReDim wss1.DestRooms(0)
                wss1.DestRooms(0) = MyRoomID
                'wss1.Purpose = "作業"

                Dim a As String = ws.RegistEnter(wss1)
                Dim b As String = ws.GateEnter(wss1, MyRoomID)
                EnterdPersons.Add(wss1.CardID, wss1.CardID.ToString)
                TurnOnOffEmptyLamp(False)
            End If

        Else '退室の場合
            Dim j As Integer = ws.CountEnteredPerson(MyRoomID)
            Dim rtval As String = ws.RegistExit(wss1, MyRoomID)
            EnterdPersons.Remove(wss1.CardID.ToString)
            If EnterdPersons.Count = 0 Then
                TurnOnOffEmptyLamp(True)
            End If
        End If
        '扉を解錠するコード
        Me.DoorUnLock()
        Me.Label2.Text = "解錠中"
        Me.Timer2.Enabled = True
        '
    End Sub

    Private Sub ReadMyRooms()
        MyRoomID = My.Computer.FileSystem.ReadAllText("MyRoomID.txt")
    End Sub

    '解錠後に指定時間内に扉が開かなかった時は施錠する
    Private Sub Timer2_Tick(sender As Object, e As EventArgs) Handles Timer2.Tick
        Me.DoorLock()
        'Me.Label2.Text = ""
        Me.Timer2.Enabled = False
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        MyRoomID = Me.TextBox3.Text
    End Sub

    Private Sub AxEng1_ValueChanged(sender As Object, e As AxFAENGINELib5._DFAEngineEvents_ValueChangedEvent) Handles AxEng1.ValueChanged
        Dim tagname() As String = e.tagPath.Split(".")
        If tagname.Length <> 3 Then Exit Sub
        Select Case tagname(2)
            Case "UNLOCK"
                If e.value Then
                    FLock = False
                    Me.Label3.Text = "解錠中"
                End If
            Case "LOCK"
                If e.value Then
                    FLock = True
                    Me.Label3.Text = "施錠中"
                End If
            Case "CLOSE"
                If e.value Then
                    '扉が閉じた直後に施錠する
                    Me.DoorLock()
                    Me.Label4.Text = "扉クローズ"
                Else
                    '扉が開けられたら施錠タイマーを解除する
                    Me.Timer2.Enabled = False
                    Me.Label4.Text = "扉オープン"
                End If
            Case "CONFIRM"
                If EnterdPersons.Count > 0 Then
                    ExecRoomEmpty()
                End If
        End Select
    End Sub



    Private Sub DoorLock()
        If Not FLock Then
            Me.AxEng1.WriteVal("U03.F01.OLOCK", True)
            Me.Label2.Text = ""
            Me.Label1.Text = ""
        End If
    End Sub

    Private Sub DoorUnLock()
        If FLock Then
            Me.AxEng1.WriteVal("U03.F01.OUNLOCK", True)
        End If
    End Sub

    Private Sub ExecRoomEmpty()
        Do
            If EnterdPersons.Count = 0 Then Exit Do
            Dim CardID = EnterdPersons.Item(1)

            Dim wss1 As WebReference.PersonInfo = ws.GetPersonInfo(CardID)
            Dim rtval As String = ws.RegistExit(wss1, MyRoomID)

            EnterdPersons.Remove(CardID.ToString)
        Loop
    End Sub

    Private Sub TurnOnOffEmptyLamp(ByVal a As Boolean)
        Me.AxEng1.WriteVal("U03.F01.EMPTY", a)
    End Sub
End Class
