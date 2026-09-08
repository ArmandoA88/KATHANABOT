Public NotInheritable Class DefaultLootItems
    Public Const Version As Integer = 1

    Public Shared Function Create() As List(Of String)
        Return New List(Of String) From {
            "Kindjal of Thunder",
            "Kindjal of Deadly Poison",
            "Ara of Deadly Poison",
            "Strong Ara",
            "Bisa Dantaka",
            "Talwar of Origin Warrior",
            "Talwar of Warrior",
            "Strong Pattra",
            "Pattra of Curse",
            "Rauti Kukri",
            "Hima Kinjar",
            "Suraka of the Muscle",
            "Suraka of the Wild Beast",
            "Trizika of Life",
            "Trizika of Destruction",
            "Yakatu Engkus",
            "Kisu of Origin Warrior",
            "Strong Kisu",
            "Agni Kisu",
            "Hirana of Deadly Poison",
            "Lu Berdysh",
            "Hima Berdysh",
            "Strong Berdysh",
            "Dead Person’s Rohiparaz",
            "Mara’s Kaja",
            "Tapas Lagda",
            "Hima Ghana",
            "Ghana of Muscle",
            "Ghana of Mantra",
            "Mara’s Nakhara",
            "Tapas Karasna",
            "Karasuna of Gushu",
            "Lu Vajradhara",
            "Agni Vajradhara",
            "Vajra of the Dead",
            "Hima Zalas",
            "Rauti Zalas",
            "Agni Vera",
            "Pataka of Flame",
            "Hard Hastacapa",
            "Hastacapa of Deadly Poison",
            "Agni Urnacapa",
            "Rada Bakurakava of Gurapa",
            "Bahamut’s Jirastra",
            "Turate of Adana",
            "Shuipara of Gurapa",
            "Bahamut’s Gana",
            "Achada Armor of Adhana",
            "Varman Armor of Grava",
            "Bahamut’s Zvas",
            "Achada Pants of Adana",
            "Varman Pants of Gurapa",
            "Bahamut’s Nakha",
            "Achada Glove of Adana",
            "Baruman Gloves of Gurapa",
            "Bahamut’s Patika",
            "Leather Upana of Adana",
            "Arapada of Adana",
            "Leather Hira of Caulitara",
            "Rohachavi of Life",
            "Rohachavi of Spells",
            "Savitri Phalaka",
            "Yakatu Phalaka",
            "Wild Carman",
            "Karna of Brave",
            "Karna of Magic",
            "Karna of Sorcery",
            "Karna of Life",
            "Bangle of Sorcery",
            "Strong Kamvu",
            "Kamvu of Spell",
            "Ran of Brave",
            "Ran of Luck",
            "Ran of Life",
            "Hima Mudra",
            "Rucaka of Wind",
            "Rucaka of Warrior",
            "Rucaka of Magic",
            "Rucaka of Life",
            "Agni Rucaka",
            "Strong Dechanda"
        }
    End Function

    Public Shared Function Merge(existing As IEnumerable(Of String)) As List(Of String)
        Dim result As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each name In If(existing, Array.Empty(Of String)()).Concat(Create())
            Dim cleaned = If(name, "").Trim()
            Dim key = cleaned.Replace(ChrW(&H2019), "'"c)
            If cleaned <> "" AndAlso seen.Add(key) Then result.Add(cleaned)
        Next
        Return result
    End Function
End Class
