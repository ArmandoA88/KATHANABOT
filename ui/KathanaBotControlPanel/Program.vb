Imports System.IO
Imports System.Reflection

Public Enum BotEdition
    Full
    Lite
End Enum

Friend Module Program
    Friend Const StartupNotice As String =
"ENGLISH" & vbCrLf &
"Hey, I need your feedback! If you have ideas to improve the bot or make it better and easier to use, please let me know. On Discord, send a direct message to mando1545." & vbCrLf &
"I might not respond, but I will read all feedback and improvement ideas. I won't respond to troubleshooting messages; I will make a troubleshooting video sooner or later. Thank you for your support!" & vbCrLf & vbCrLf &
"ESPAÑOL" & vbCrLf &
"¡Hola! Necesito tus comentarios. Si tienes ideas para mejorar el bot o hacerlo mejor y más fácil de usar, házmelo saber. En Discord, envíame un mensaje directo a mando1545." & vbCrLf &
"Puede que no responda, pero leeré todos los comentarios y las ideas para mejorar. No responderé mensajes sobre solución de problemas; tarde o temprano haré un video al respecto. ¡Gracias por tu apoyo!" & vbCrLf & vbCrLf &
"TAGALOG" & vbCrLf &
"Hey, kailangan ko ang feedback mo! Kung may mga ideya ka para mapahusay ang bot o gawin itong mas maayos at mas madaling gamitin, ipaalam mo sa akin. Sa Discord, mag-direct message ka kay mando1545." & vbCrLf &
"Maaaring hindi ako sumagot, pero babasahin ko ang lahat ng feedback at mga ideya para sa pagpapahusay. Hindi ako sasagot sa mga mensahe tungkol sa troubleshooting; gagawa ako ng troubleshooting video balang araw. Maraming salamat sa iyong suporta!"

    ' Kept on hold for possible future reuse; this is no longer shown at startup.
    Friend Const PreviousStartupNotice As String =
"Espero que esto te ayude a disfrutar más el juego. Recuerda que el tiempo aquí es tiempo que no usas en lo importante, pero por ahora diviértete. Te quiero mucho, SAITAMA." & vbCrLf & vbCrLf &
"I hope this helps you enjoy the game more. Remember that time here is time you are not using for important things, but for now, have fun. I love you very much, SAITAMA." & vbCrLf & vbCrLf &
"Sana ay makatulong ito para mas ma-enjoy mo ang laro. Tandaan mo na ang oras dito ay oras na hindi mo nagagamit sa mahahalagang bagay, pero sa ngayon, magsaya ka. Mahal na mahal kita, SAITAMA."

    <STAThread()>
    Friend Sub Main(args As String())
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledDomainException
        AddHandler Application.ThreadException, AddressOf OnUnhandledThreadException
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        Try
            Velopack.VelopackApp.Build().Run()
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            OcrReader.PrewarmAsync()
            Application.Run(New Form1())
        Catch ex As Exception
            LogStartupCrash(ex)
            Throw
        End Try
    End Sub

    Private Sub OnUnhandledDomainException(sender As Object, e As UnhandledExceptionEventArgs)
        LogStartupCrash(TryCast(e.ExceptionObject, Exception))
    End Sub

    Private Sub OnUnhandledThreadException(sender As Object, e As Threading.ThreadExceptionEventArgs)
        LogStartupCrash(e.Exception)
    End Sub

    ' Without this, a crash before the main window ever opens (or on a non-UI thread) leaves no
    ' trace at all - the process just exits and the user has no way to tell us why. crash.log sits
    ' next to the exe so it survives even a startup failure inside Form1's own constructor.
    Private Sub LogStartupCrash(ex As Exception)
        Try
            Dim exeDirectory As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            Dim logPath As String = Path.Combine(If(String.IsNullOrEmpty(exeDirectory), ".", exeDirectory), "crash.log")
            Dim entry As String = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {If(ex Is Nothing, "(unknown exception)", ex.ToString())}{Environment.NewLine}{Environment.NewLine}"
            File.AppendAllText(logPath, entry)
        Catch
        End Try
    End Sub

End Module
