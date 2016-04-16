Imports System.Diagnostics

<CLSCompliant(True)>
Public Class EventLogger


    Private intFileNumber As Integer = FreeFile()

    Public Sub New()
        'default constructor
    End Sub

    '************************************************************
    'NAME: WriteToEventLog
    'PURPOSE: Write to Event Log
    'PARAMETERS: Entry - Value to Write
    ' AppName - Name of Client Application. Needed
    ' because before writing to event log, you must
    ' have a named EventLog source.
    ' EventType - Entry Type, froEventLogEntryType
    ' Structure e.g., EventLogEntryType.Warning,
    ' EventLogEntryType.Error
    ' LogNam1e: Name of Log (System, Application;
    ' Security is read-only) If you
    ' specify a non-existent log, the log will be
    ' created
    'RETURNS: True if successful
    '*************************************************************

    Public Function WriteToEventLog(ByVal entry As String, Optional ByVal eventType As EventLogEntryType = EventLogEntryType.Information) As Boolean

        Dim objEventLog As New EventLog
        Dim strLogFile As String

        ' Write to Event Logs
        Try

            ''register the Application as an Event Source
            'If Not EventLog.SourceExists(My.Settings.AdapterName) Then
            ' EventLog.CreateEventSource(My.Settings.AdapterName, My.Settings.AdapterName + "_Log")
            'End If

            '' Log the entry with the Windows Event Logs.
            'objEventLog.Source = My.Settings.AdapterName
            'entry = My.Settings.AdapterName + ": " + entry
            'objEventLog.WriteEntry(entry, eventType)

            ' Always write to text log file in application directory
            strLogFile = My.Settings.ProcessLogPath & "ParseFeed_Log_File_" & My.Settings.ProcessName & Format(Now, "_yyyy_MM_dd") & ".txt"
            FileOpen(intFileNumber, strLogFile, OpenMode.Append)
            Dim strDate As String = Format(Now, "yyyy-MM-dd")
            Dim strTimestamp As String = Format(Now, "HH.mm.ss.ffffff")
            Dim strEntryType As String = ""
            Select Case eventType
                Case EventLogEntryType.Information
                    strEntryType = "Information"
                Case EventLogEntryType.Error
                    strEntryType = "Error"
                Case EventLogEntryType.FailureAudit
                    strEntryType = "Failure Audit"
                Case EventLogEntryType.SuccessAudit
                    strEntryType = "Sucsess Audit"
                Case EventLogEntryType.Warning
                    strEntryType = "Warning"
                Case Else
                    strEntryType = "Unknown"
            End Select

            PrintLine(intFileNumber, strDate & "." & strTimestamp & ", " & strEntryType & ", " & entry)
            FileClose(intFileNumber)

            Return True

        Catch Ex As Exception

            Return False

        End Try

    End Function

End Class
