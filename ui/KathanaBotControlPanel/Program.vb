Public Enum BotEdition
    Full
    Lite
End Enum

Friend Module Program
    Friend Const StartupNotice As String =
"Espero que esto te ayude a disfrutar más el juego. Recuerda que el tiempo aquí es tiempo que no usas en lo importante, pero por ahora diviértete. Te quiero mucho, SAITAMA." & vbCrLf & vbCrLf &
"I hope this helps you enjoy the game more. Remember that time here is time you are not using for important things, but for now, have fun. I love you very much, SAITAMA." & vbCrLf & vbCrLf &
"Sana ay makatulong ito para mas ma-enjoy mo ang laro. Tandaan mo na ang oras dito ay oras na hindi mo nagagamit sa mahahalagang bagay, pero sa ngayon, magsaya ka. Mahal na mahal kita, SAITAMA."

    <STAThread()>
    Friend Sub Main(args As String())
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        OcrReader.PrewarmAsync()
        Application.Run(New Form1())
    End Sub

End Module
