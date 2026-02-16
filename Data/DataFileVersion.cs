[System.Serializable]
public struct DataFileVersion
{
    public bool IsNull { get { return string.IsNullOrEmpty(fileName); } }

    public string fileName;
    public int version;
}