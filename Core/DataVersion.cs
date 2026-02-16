using System.Collections.Generic;

[System.Serializable]
public class DataVersion
{
    public int version;
    public List<DataFileVersion> dataFileVersion;
}