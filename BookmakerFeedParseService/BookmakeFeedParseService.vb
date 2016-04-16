Imports System.ServiceProcess
Imports System.Text
Public Class BookmakeFeedParseService
    Inherits System.ServiceProcess.ServiceBase

    Private worker As New Worker()

    Protected Overrides Sub OnStart(ByVal args() As String)

        Dim wt As System.Threading.Thread
        Dim ts As System.Threading.ThreadStart
        gobjEvent.WriteToEventLog("Service Start Banner:    ************************************************")
        gobjEvent.WriteToEventLog("Service Start Banner:    *       BookmakerFeedParseService STARTING       *")
        gobjEvent.WriteToEventLog("Service Start Banner:    ************************************************")
        gobjEvent.WriteToEventLog("Windows Service OnStart method starting service.")

        ts = AddressOf worker.DoWork
        wt = New System.Threading.Thread(ts)

        wt.Start()

    End Sub

    Protected Overrides Sub OnStop()
        worker.StopWork()
    End Sub

End Class

Public Class Worker

    Private m_thMain As System.Threading.Thread
    Private m_booMustStop As Boolean = False
    Private m_rndGen As New Random(Now.Millisecond)
    Private Shared stdOutput As StringBuilder = Nothing
    Private Shared stdNumOutputLines As Integer = 0
    Private Shared errOutput As StringBuilder = Nothing
    Private Shared errNumOutputLines As Integer = 0
    Private intFileNumber As Integer = FreeFile()
    Public Sub StopWork()

        m_booMustStop = True

        gobjEvent.WriteToEventLog("Service Stopping Banner: ************************************************")
        gobjEvent.WriteToEventLog("Service Stopping Banner: *       BookmakerFeedParseService STOPPED        *")
        gobjEvent.WriteToEventLog("Service Stopping Banner: ************************************************")

        If Not m_thMain Is Nothing Then

            If Not m_thMain.Join(100) Then

                m_thMain.Abort()

            End If

        End If

    End Sub
    Public Sub DoWork()

        '----------------------------------------------------------'
        'Purpose:   Worker thread.
        '----------------------------------------------------------'

        m_thMain = System.Threading.Thread.CurrentThread

        Dim i As Integer = m_rndGen.Next
        Dim blnReturnStatus As Boolean
        Dim intMins As Integer
        Dim intAdapterCycleEveryMillisecs As Integer = My.Settings.ProcessCycleEverySecs * 1000

        m_thMain.Name = "Thread" & i.ToString
        gobjEvent.WriteToEventLog("Windows worker thread : " + m_thMain.Name + " created.")

        ' Write log entries for configuration settings
        gobjEvent.WriteToEventLog("WorkerThread : Name : " + My.Settings.ProcessName)
        gobjEvent.WriteToEventLog("WorkerThread : Path: " + My.Settings.ProcessFullPathFilename)
        gobjEvent.WriteToEventLog("WorkerThread : Arguments : " + My.Settings.ProcessArguments)
        gobjEvent.WriteToEventLog("WorkerThread : Cycle every (secs) : " + My.Settings.ProcessCycleEverySecs.ToString)
        gobjEvent.WriteToEventLog("WorkerThread : Cycle every (millisecs) : " + intAdapterCycleEveryMillisecs.ToString)

        ' Convert seconds to millisecs
        gintMaximumExecThressholdMillisecs = My.Settings.ProcessMaxExecRuntimeSecs * 1000
        gintKillRuntimeThressholdMillisecs = My.Settings.ProcessKillRuntimeSecs * 1000

        ' Check kill runtime is sensible
        If gintKillRuntimeThressholdMillisecs < gintMaximumExecThressholdMillisecs Then
            gintKillRuntimeThressholdMillisecs = gintMaximumExecThressholdMillisecs + 5000
        End If

        gobjEvent.WriteToEventLog("WorkerThread : Maximum Execution runtime thresshold from configuration file (Secs): " + My.Settings.ProcessMaxExecRuntimeSecs.ToString)
        gobjEvent.WriteToEventLog("WorkerThread : Maximum Execution runtime thresshold set to (MilliSecs): " + gintMaximumExecThressholdMillisecs.ToString)
        gobjEvent.WriteToEventLog("WorkerThread : Kill runtime thresshold from configuration file Secs): " + My.Settings.ProcessKillRuntimeSecs.ToString)
        gobjEvent.WriteToEventLog("WorkerThread : Kill runtime thresshold set to (MilliSecs): " + gintKillRuntimeThressholdMillisecs.ToString)

        While Not m_booMustStop

            ' Call start process and set status
            blnReturnStatus = StartProcess()

            ' Check status and issue warning
            If blnReturnStatus = False Then
                gobjEvent.WriteToEventLog("WorkerThread : Process returned failed status, service will continue", EventLogEntryType.Warning)
            End If

            '-------------------------------------------------
            '-  Issue heartbeat message every service cycle  -
            '-------------------------------------------------
            If intMins = 0 Then
                gobjEvent.WriteToEventLog("Windows worker thread : Heartbeat.......")
            End If

            '-------------------------------------------------
            '-  Now sleep, you beauty.                       -
            '-------------------------------------------------
            System.Threading.Thread.Sleep(intAdapterCycleEveryMillisecs)

        End While

    End Sub
    Function StartProcess() As Boolean

        ' Define static variables shared by class methods.

        Dim myProcess As New Process
        Dim intElapsedTimeMillisecs As Integer = 0
        Dim beforeTS As TimeSpan
        Dim afterTS As TimeSpan

        ' Reset output fields
        stdOutput = Nothing
        stdNumOutputLines = 0
        errOutput = Nothing
        errNumOutputLines = 0

        Try

            Dim startInfo As New System.Diagnostics.ProcessStartInfo
            startInfo.FileName = """" & Environment.SystemDirectory & "\cmd.exe" & """"

            If My.Computer.Info.OSVersion >= "6" Then  ' Windows Vista or higher

                ' required to invoke UAC
                startInfo.Verb = "runas"

            End If

            Dim parameters As String = My.Settings.ProcessArguments
            startInfo.Arguments = "/C """"" & My.Settings.ProcessFullPathFilename & """ " & parameters & """"
            startInfo.CreateNoWindow = True

            If My.Settings.RedirectProcessOutput = True Then

                ' Handle standard output async
                startInfo.RedirectStandardOutput = True
                stdOutput = New StringBuilder()
                AddHandler myProcess.OutputDataReceived, AddressOf StandardOutputHandler

                ' Handle error output async
                startInfo.RedirectStandardError = True
                errOutput = New StringBuilder()
                AddHandler myProcess.ErrorDataReceived, AddressOf ErrorOutputHandler

            Else

                startInfo.RedirectStandardOutput = False
                startInfo.RedirectStandardError = False
            End If

            startInfo.WindowStyle = ProcessWindowStyle.Hidden

            startInfo.UseShellExecute = False

            gobjEvent.WriteToEventLog("StartProcess : Starting : " + My.Settings.ProcessFullPathFilename)

            myProcess.StartInfo = startInfo

            myProcess.Start()

            gobjEvent.WriteToEventLog("StartProcess : Started Process.....")



            If My.Settings.RedirectProcessOutput = True Then

                ' Start the asynchronous read of the output streams.
                myProcess.BeginOutputReadLine()
                myProcess.BeginErrorReadLine()

            End If

            ' Now sleep ....you beauty....will exit when complete or thresshold reached
            myProcess.WaitForExit(gintMaximumExecThressholdMillisecs)

            ' Check whether process completed of timed out i.e. hit gintMaximumExecThressholdMillisecs
            If Not myProcess.HasExited Then

                intElapsedTimeMillisecs = gintMaximumExecThressholdMillisecs

                While intElapsedTimeMillisecs < gintKillRuntimeThressholdMillisecs

                    ' If we have breached the Kill thresshold....
                    If intElapsedTimeMillisecs > gintKillRuntimeThressholdMillisecs Then

                        gobjEvent.WriteToEventLog("StartProcess : Kill thresshold reached. Process ran for (Millisecs): " & intElapsedTimeMillisecs.ToString)
                        gobjEvent.WriteToEventLog("StartProcess : Kill thresshold reached. User Timestamp activity: " & myProcess.UserProcessorTime.ToString & "  Total Processing activity: " & myProcess.TotalProcessorTime.ToString)

                        Exit While

                    Else

                        ' Ok, over the Maximum Execution thresshold....check for activity
                        beforeTS = myProcess.UserProcessorTime
                        gobjEvent.WriteToEventLog("StartProcess : Activity check in progress....process will check activity over (Millisecs) " & cintCheckProcessActiveDelayMillisecs.ToString)

                        ' Sleep again to check on activity
                        System.Threading.Thread.Sleep(cintCheckProcessActiveDelayMillisecs)
                        intElapsedTimeMillisecs = intElapsedTimeMillisecs + cintCheckProcessActiveDelayMillisecs

                        If myProcess.HasExited Then
                            gobjEvent.WriteToEventLog("StartProcess : Process HasExited after pausing for activity check elapsed time (millisecs): " & intElapsedTimeMillisecs.ToString)
                            Exit While
                        End If

                        ' Continue with checks
                        afterTS = myProcess.UserProcessorTime
                        gobjEvent.WriteToEventLog("StartProcess : Activity check. beforeTS is: " & beforeTS.ToString & ". afterTS is: " & afterTS.ToString)

                        ' Process looks like it has hung...
                        If beforeTS = afterTS Then
                            gobjEvent.WriteToEventLog("StartProcess : Activity check, Process looks like it has hung. User processor time: " & myProcess.UserProcessorTime.ToString)
                            Exit While
                        Else
                            ' Report activity
                            gobjEvent.WriteToEventLog("StartProcess : Activity check, Max Exec reached but process still looks busy. User processor time: " & myProcess.UserProcessorTime.ToString)

                        End If

                    End If

                    ' Check for process exit.
                    If myProcess.HasExited Then
                        gobjEvent.WriteToEventLog("StartProcess : Process HasExited after While loop for elapsed time (millisecs): " & intElapsedTimeMillisecs.ToString)
                        gobjEvent.WriteToEventLog("StartProcess : Process HasExited after While loop. User Timestamp activity: " & myProcess.UserProcessorTime.ToString & "  Total Processing activity: " & myProcess.TotalProcessorTime.ToString)
                        Exit While
                    End If

                End While

            Else

                ' Process has finished whilst waiting WaitForExit
                gobjEvent.WriteToEventLog("StartProcess : Process HasExited after WaitForExit. User Timestamp activity: " & myProcess.UserProcessorTime.ToString & "  Total Processing activity: " & myProcess.TotalProcessorTime.ToString)

            End If

            ' Write output lines
            If My.Settings.RedirectProcessOutput = True Then

                If Not String.IsNullOrEmpty(stdNumOutputLines) And stdNumOutputLines > 0 Then

                    ' Write the output to the consolelog.
                    gobjEvent.WriteToEventLog("StartProcess : Standard Output lines " & stdNumOutputLines.ToString)
                    gobjEvent.WriteToEventLog("StartProcess : Standard Output text " & stdOutput.ToString)

                Else

                    gobjEvent.WriteToEventLog("StartProcess : No Standard Output lines .")

                End If

                If Not String.IsNullOrEmpty(errNumOutputLines) And errNumOutputLines > 0 Then

                    ' Write the formatted and sorted output to the console.
                    gobjEvent.WriteToEventLog("StartProcess : Error Output lines " & errNumOutputLines.ToString)
                    gobjEvent.WriteToEventLog("StartProcess : Error Output text " & errOutput.ToString)
                Else
                    gobjEvent.WriteToEventLog("StartProcess : No Error Output lines .")
                End If

            End If

            ' Check return code
            If myProcess.ExitCode <> 0 Then

                Dim strErrorFile As String
                gobjEvent.WriteToEventLog("StartProcess : Process has returned non zero exit code. Exit Code: " & myProcess.ExitCode.ToString, EventLogEntryType.Error)
                gobjEvent.WriteToEventLog("Service Error Banner: ******************************************************")
                gobjEvent.WriteToEventLog("Service Error Banner: *      N O N   Z E R O    R E T U R N   C O D E  !!! *")
                gobjEvent.WriteToEventLog("Service Error Banner: ******************************************************")

                ' Check we want to write errors
                If My.Settings.ErrorFIleOutput Then
                    Try
                        strErrorFile = My.Settings.ErrorPath & "Parse_Feed_Error_File_" & My.Settings.ProcessName & Format(Now, "_yyyy_MM_dd_HH-mm-ss") & ".txt"
                        FileOpen(intFileNumber, strErrorFile, OpenMode.Output)
                        Dim strDate As String = Format(Now, "yyyy-MM-dd")
                        Dim strTimestamp As String = Format(Now, "HH.mm.ss.ffffff")
                        PrintLine(intFileNumber, "*-----------------------------------------------*")
                        PrintLine(intFileNumber, "*     BookmakerFeedParseService Error Report    *")
                        PrintLine(intFileNumber, "*-----------------------------------------------*")
                        PrintLine(intFileNumber, "Date/Timestamp: " & strDate & "." & strTimestamp)
                        PrintLine(intFileNumber, "Name: " & My.Settings.ProcessName)
                        PrintLine(intFileNumber, "Arguments: " & My.Settings.ProcessArguments)
                        PrintLine(intFileNumber, "Path: " & My.Settings.ProcessFullPathFilename)
                        PrintLine(intFileNumber, "Cycle: " & My.Settings.ProcessCycleEverySecs.ToString)
                        PrintLine(intFileNumber, "Process Max Exec Thresshold: " & My.Settings.ProcessMaxExecRuntimeSecs.ToString)
                        PrintLine(intFileNumber, "Process Kill Thresshold: " & My.Settings.ProcessKillRuntimeSecs.ToString)
                        PrintLine(intFileNumber, "Exit Code:      " & myProcess.ExitCode.ToString)
                        PrintLine(intFileNumber, " ")
                        PrintLine(intFileNumber, " ")
                        PrintLine(intFileNumber, "*-----------------*")
                        PrintLine(intFileNumber, "* Standard Output *")
                        PrintLine(intFileNumber, "*-----------------*")
                        PrintLine(intFileNumber, stdOutput.ToString)
                        PrintLine(intFileNumber, " ")
                        PrintLine(intFileNumber, " ")
                        PrintLine(intFileNumber, "*-----------------*")
                        PrintLine(intFileNumber, "*   Error Output  *")
                        PrintLine(intFileNumber, "*-----------------*")
                        PrintLine(intFileNumber, errOutput.ToString)
                        PrintLine(intFileNumber, " ")
                        PrintLine(intFileNumber, "              * End Of Report *")
                        FileClose(intFileNumber)
                    Catch ex As Exception
                        gobjEvent.WriteToEventLog("StartProcess : Problem creating error file. Exception message : " & ex.Message)
                    End Try

                End If

            Else

                gobjEvent.WriteToEventLog("StartProcess : Process has completed Successfully. Exit Code 0.")

            End If


            ' Check whether process has actually ended
            If Not myProcess.HasExited Then
                Dim strErrorFile As String
                myProcess.Kill()
                myProcess.Dispose()
                gobjEvent.WriteToEventLog("StartProcess : Process has been killed as not completed.", EventLogEntryType.Error)
                gobjEvent.WriteToEventLog("Service Error Banner: *****************************************")
                gobjEvent.WriteToEventLog("Service Error Banner: *      P R O C E S S    K I L L E D !!! *")
                gobjEvent.WriteToEventLog("Service Error Banner: ****************************************")

                Try
                    strErrorFile = My.Settings.ErrorPath & "Parse_Feed_KilledProcess_File_" & My.Settings.ProcessName & Format(Now, "_yyyy_MM_dd_HH-mm-ss") & ".txt"
                    FileOpen(intFileNumber, strErrorFile, OpenMode.Output)
                    Dim strDate As String = Format(Now, "yyyy-MM-dd")
                    Dim strTimestamp As String = Format(Now, "HH.mm.ss.ffffff")
                    PrintLine(intFileNumber, "*-----------------------------------------------*")
                    PrintLine(intFileNumber, "*     BookmakerFeedParseService Kill Report     *")
                    PrintLine(intFileNumber, "*-----------------------------------------------*")
                    PrintLine(intFileNumber, "Date/Timestamp: " & strDate & "." & strTimestamp)
                    PrintLine(intFileNumber, "Name: " & My.Settings.ProcessName)
                    PrintLine(intFileNumber, "Arguments: " & My.Settings.ProcessArguments)
                    PrintLine(intFileNumber, "Path: " & My.Settings.ProcessFullPathFilename)
                    PrintLine(intFileNumber, "Cycle: " & My.Settings.ProcessFullPathFilename.ToString)
                    PrintLine(intFileNumber, "Process Max Exec Thresshold: " & My.Settings.ProcessMaxExecRuntimeSecs.ToString)
                    PrintLine(intFileNumber, "Process Kill Thresshold: " & My.Settings.ProcessKillRuntimeSecs.ToString)
                    PrintLine(intFileNumber, "Exit Code:      " & myProcess.ExitCode.ToString)
                    PrintLine(intFileNumber, " ")
                    PrintLine(intFileNumber, " ")
                    PrintLine(intFileNumber, "*-----------------*")
                    PrintLine(intFileNumber, "* Standard Output *")
                    PrintLine(intFileNumber, "*-----------------*")
                    PrintLine(intFileNumber, stdOutput.ToString)
                    PrintLine(intFileNumber, " ")
                    PrintLine(intFileNumber, " ")
                    PrintLine(intFileNumber, "*-----------------*")
                    PrintLine(intFileNumber, "*   Error Output  *")
                    PrintLine(intFileNumber, "*-----------------*")
                    PrintLine(intFileNumber, errOutput.ToString)
                    PrintLine(intFileNumber, " ")
                    PrintLine(intFileNumber, "              * End Of Report *")
                    FileClose(intFileNumber)
                Catch ex As Exception
                    gobjEvent.WriteToEventLog("StartProcess : Problem creating kill file. Exception message : " & ex.Message)
                End Try
                Return False

            End If

            ' Dispose of process

            myProcess.Dispose()

        Catch ex As Exception

            If Not myProcess.HasExited Then myProcess.Kill()
            myProcess.Dispose()
            gobjEvent.WriteToEventLog("StartProcess : Process has been killed, general error : " & ex.Message, EventLogEntryType.Error)
            Return False

        End Try

        Return True

    End Function
    Private Shared Sub StandardOutputHandler(ByVal sendingProcess As Object, ByVal outLine As DataReceivedEventArgs)

        ' Collect the output.
        If Not String.IsNullOrEmpty(outLine.Data) Then
            stdNumOutputLines += 1

            ' Add the text to the collected output.
            stdOutput.Append(Environment.NewLine + "[" + stdNumOutputLines.ToString() + "] - " + outLine.Data)
        End If

    End Sub
    Private Shared Sub ErrorOutputHandler(ByVal sendingProcess As Object, ByVal outLine As DataReceivedEventArgs)

        ' Collect the output.

        If Not String.IsNullOrEmpty(outLine.Data) Then
            errNumOutputLines += 1

            ' Add the text to the collected output.
            errOutput.Append(Environment.NewLine + "[" + errNumOutputLines.ToString() + "] - " + outLine.Data)

        End If

    End Sub
End Class
