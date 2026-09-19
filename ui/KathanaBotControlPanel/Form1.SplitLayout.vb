Partial Public Class Form1
    Private Shared Function CreateResizableColumns(initialFraction As Double) As SplitContainer
        Dim split As New SplitContainer With {.Size = New Size(1200, 600), .Dock = DockStyle.Fill, .Orientation = Orientation.Vertical, .SplitterWidth = 8, .Panel1MinSize = 100, .Panel2MinSize = 100, .BackColor = Color.FromArgb(50, 85, 105)}
        Dim fraction = initialFraction
        Dim resizing = False
        Dim applySize As Action =
            Sub()
                Dim available = split.ClientSize.Width - split.SplitterWidth
                If available < split.Panel1MinSize + split.Panel2MinSize Then Return
                resizing = True
                Try
                    split.SplitterDistance = Math.Clamp(CInt(available * fraction), split.Panel1MinSize, available - split.Panel2MinSize)
                Finally
                    resizing = False
                End Try
            End Sub
        AddHandler split.SizeChanged, Sub() applySize()
        AddHandler split.SplitterMoved,
            Sub()
                If resizing Then Return
                Dim available = split.ClientSize.Width - split.SplitterWidth
                If available > 0 Then fraction = split.SplitterDistance / CDbl(available)
            End Sub
        applySize()
        Return split
    End Function
End Class
