using System;
using System.Collections.Generic;

public abstract class BaseTable<Data> : ITable where Data : IBaseTableData
{
    public virtual string FilePath { get; }
    protected Data[] tableDatas = null;
    protected Dictionary<int, Data> tableDataDic = null;
    protected Dictionary<string, Data> tableEnumIdDic = null;

    public void LoadTable(string localPath)
    {
        tableDatas = FromJsonArray(System.IO.File.ReadAllText(localPath + FilePath + ".json"));
        tableDataDic = new Dictionary<int, Data>(tableDatas.Length);
        tableEnumIdDic = new Dictionary<string, Data>(tableDatas.Length);

        for (int i = 0; i < tableDatas.Length; ++i)
        {
            tableDataDic.Add(tableDatas[i].GetId(), tableDatas[i]);

            if (string.IsNullOrEmpty(tableDatas[i].GetEnumId()))
                continue;

            tableEnumIdDic.Add(tableDatas[i].GetEnumId(), tableDatas[i]);
        }
    }

    public Data GetDataByIndex(int index)
    {
        if (index < tableDatas.Length)
            return tableDatas[index];

        return default;
    }

    public Data GetDataByID(int id)
    {
        if (tableDataDic.ContainsKey(id))
            return tableDataDic[id];

        return default;
    }

    public Data GetDataByEnumId(string enumId)
    {
        if (tableEnumIdDic == null)
            return default;

        if (tableEnumIdDic.ContainsKey(enumId))
            return tableEnumIdDic[enumId];

        return default;
    }

    public Data Find(System.Predicate<Data> predicate)
    {
        return Array.Find(tableDatas, predicate);
    }

    public Data[] FindAll(System.Predicate<Data> predicate)
    {
        return Array.FindAll(tableDatas, predicate);
    }

    public int GetDataTotalCount()
    {
        return tableDatas.Length;
    }

    private Data[] FromJsonArray(string json)
    {
        Data[] parsing = null;

        try
        {
            parsing = Newtonsoft.Json.JsonConvert.DeserializeObject<Data[]>(json);
        }
        catch (System.Exception e)
        {
            IronJade.Debug.LogError($"Parsing Error!!! => [{typeof(Data).Name}]{json}");
            IronJade.Debug.LogError(e);
            throw e;
        }

        return parsing;
    }
}