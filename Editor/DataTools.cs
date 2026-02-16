#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class DataTools
{
    private enum RowType
    {
        Include = 2,    // A, S, C
        Type,           // int, string, enum, class . . .
        Name,           // col Name
        Value,
    }

    private enum ColType
    {
        Id = 1,
        Enum_Id,
        State,
    }

    public struct ColData
    {
        public struct TypeData
        {
            public string name;
            public List<string> value;
        }

        public string include;
        public TypeData type;
        public string name;
        public List<List<string>> value;
    }

    private string excelDirectoryPath = string.Empty;
    private string cloudDirectoryPath = string.Empty;
    private string commonEnumPath = string.Empty;
    private string clientJsonPath = string.Empty;
    private string serverJsonPath = string.Empty;
    private string cloudJsonPath = string.Empty;
    private string clientVersionJsonPath = string.Empty;
    private string serverVersionJsonPath = string.Empty;
    private string clientTempletPath = string.Empty;
    private string clientTempletResultPath = string.Empty;
    private string cloudTempletResultPath = string.Empty;
    private string clientTempletUtilityResultPath = string.Empty;
    private string clientTempletEnumResultPath = string.Empty;
    private HashSet<string> includeTypes = new HashSet<string>    // 지정 가능한 타입들
        {
            "enum",
            "int",
            "float",
            "string",
            "class:",
            "enum:",
            "int[]",
            "float[]",
            "string[]",
            "class[]:",
            "enum[]:",
            "decimal[]",
            "class[][]:",
        };

    // [엑셀이름][Row][Col][(Inclue,Type,Name,Value)]
    private Dictionary<string, Dictionary<int, Dictionary<int, ColData>>> allExcelDatas = new Dictionary<string, Dictionary<int, Dictionary<int, ColData>>>();
    private Dictionary<string, Dictionary<int, Dictionary<int, ColData>>> dregDropExcelDatas = new Dictionary<string, Dictionary<int, Dictionary<int, ColData>>>();

    // [ENUM_ID][Row 한 줄]
    private Dictionary<string, Dictionary<int, ColData>> allExcelEnumIdDatas = new Dictionary<string, Dictionary<int, ColData>>();

    // [Enum이름][값(이름과 번호)]
    private Dictionary<string, Dictionary<string, int>> commonEnum = new Dictionary<string, Dictionary<string, int>>();

    private HashSet<string> inclueENUM_IDExcels = new HashSet<string>()
    {
        "Mission",
        "StoryQuest",
        "DailyQuest"
    };

    [MenuItem("Tools/Table/Parse", false, priority = 0)]
    public static void TableParse()
    {
        var dataTools = new DataTools();
        dataTools.Process().Forget();
    }

    /// 메시지 팝업
    /// </summary>
    private async UniTask ShowMessage(string message)
    {
        Debug.Log(message);
    }

    /// <summary>
    /// 툴 사용에 필요한 경로를 설정한다.
    /// </summary>
    private void SetPath()
    {
        excelDirectoryPath = Application.dataPath.Replace("/Assets", "/Table");
        cloudDirectoryPath = Application.dataPath.Replace("/Assets", "/CloudCode/Project/Table");

        // CommonEnum.cs 경로
        commonEnumPath = $"{Application.dataPath}/Scripts/Common/Enum/CommonEnum.cs";

        // clientJson 파일 경로
        clientJsonPath = $"{excelDirectoryPath}/ClientJson/";

        // serverJson 파일 경로
        serverJsonPath = $"{excelDirectoryPath}/ServerJson/";

        // cloudJson 파일 경로 (cs)
        cloudJsonPath = $"{cloudDirectoryPath}";

        // clientVersionJson 파일 경로
        clientVersionJsonPath = $"{excelDirectoryPath}/ClientJson/";

        // serverVersionJson 파일 경로
        serverVersionJsonPath = $"{excelDirectoryPath}/ServerJson/";

        // 클라이언트 템플릿 cs파일 경로
        clientTempletPath = $"{excelDirectoryPath}/GeneratorTemplet/";

        // 클라이언트 템플릿 cs파일 제너레이트 결과 경로
        clientTempletResultPath = $"{Application.dataPath}/Scripts/Common/Table/Data/";

        // 클라우드 템플릿 cs파일 제너레이트 결과 경로
        cloudTempletResultPath = $"{cloudDirectoryPath}/Data/";


        clientTempletUtilityResultPath = $"{Application.dataPath}/Scripts/Common/Table/Utility/";
        clientTempletEnumResultPath = $"{Application.dataPath}/Scripts/Common/Table/Enum/";
    }

    /// <summary>
    /// 데이터 파싱 프로세스
    /// </summary>
    public async UniTask Process()
    {
        SetPath();

        if (!await ReadCommonEnum())
            return;

        if (!await ReadAllExcels())
            return;

        if (!await CheckingDependanceExcel())
            return;

        if (!await JsonParse())
            return;

        if (!await ClientScriptGenerator())
            return;

        await ShowMessage("데이터 파싱이 모두 완료 되었습니다.");
    }

    /// <summary>
    /// CommonEnum을 읽어온다.
    /// </summary>
    public async UniTask<bool> ReadCommonEnum()
    {
        try
        {
            await ShowMessage("CommonEnum을 읽는 중입니다.");

            // 텍스트 파일 읽기
            if (!File.Exists(commonEnumPath))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("CommonEnum을 읽어오지 못 했습니다.");
                sb.Append($"경로 : {commonEnumPath}");
                await ShowMessage(sb.ToString());
                return false;
            }

            string fileContent = File.ReadAllText(commonEnumPath);

            // 주석 제거 (// 이후의 모든 텍스트 제거)
            fileContent = Regex.Replace(fileContent, @"//.*", "").Trim();

            // 정규 표현식으로 enum 추출
            string enumPattern = @"public enum (\w+)\s*{\s*([^}]*)\s*}";
            MatchCollection matches = Regex.Matches(fileContent, enumPattern);
            commonEnum.Clear();

            foreach (Match match in matches)
            {
                string enumName = match.Groups[1].Value;
                string[] enumEntries = match.Groups[2].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                int autoValue = 0;
                //var enumValues = new List<(string Name, int? Value)>();
                var enumValues = new Dictionary<string, int>();

                foreach (var entry in enumEntries)
                {
                    var parts = entry.Trim().Split('=');
                    string name = parts[0].Trim();

                    if (string.IsNullOrEmpty(name))
                        break;

                    int value = parts.Length > 1 ? int.Parse(parts[1].Trim()) : autoValue;

                    enumValues.Add(name, value);
                    autoValue = value + 1; // 값이 명시되었든, 자동으로 할당되었든 다음 값 증가
                }

                commonEnum[enumName] = enumValues;
            }

            return true;
        }
        catch (System.Exception e)
        {
            // CommonEnum을 읽어오지 못 했습니다.
            // CommonEnum에 잘못된 값이 있는지 확인 바랍니다.
            // 에러 Stack : {0}
            // 에러 Message : {0}

            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("CommonEnum을 읽어오지 못 했습니다.");
            sb.AppendLine("CommonEnum에 잘못된 값이 있는지 확인 바랍니다.");
            sb.AppendLine($"경로 : {commonEnumPath}");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }
    }

    /// <summary>
    /// 모든 엑셀을 읽는다.
    /// </summary>
    public async UniTask<bool> ReadAllExcels()
    {
        await ShowMessage("모든 엑셀 정보를 읽는 중입니다.");

        // 1. 엑셀 파일을 모두 읽는다.
        // 2. 시트 이름과 ENUM_ID로 정보를 담는다.
        DirectoryInfo excelDirectory = new DirectoryInfo(excelDirectoryPath);
        FileInfo[] excelFileInfos = excelDirectory.GetFiles("*.xlsx", SearchOption.TopDirectoryOnly);

        allExcelDatas.Clear();
        allExcelEnumIdDatas.Clear();
        dregDropExcelDatas.Clear();

        var checkExcelNames = new Dictionary<string, string>();
        var checkExcelEnumIds = new Dictionary<string, string>();

        try
        {
            foreach (var excelFileInfo in excelFileInfos)
            {
                // 테이블 이름이 !이면 읽지 않는다.
                if (excelFileInfo.Name[0].Equals('!') ||
                    excelFileInfo.Name[0].Equals('_'))
                    continue;

                string fullPath = excelFileInfo.FullName.Replace("\\", "/");
                using (var stream = File.Open(fullPath, System.IO.FileMode.Open, FileAccess.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
                    {
                        if (reader == null)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("Excel을 읽어오지 못 했습니다.");
                            sb.AppendLine("Excel이 실행 중이거나 손상 또는 확장자가 잘못 되었습니다.");
                            sb.AppendLine("(만약 파일에 문제가 없는 경우 PC를 재부팅 해보시길 바랍니다.)");
                            sb.AppendLine($"현재 Excel : {fullPath}");
                            await ShowMessage(sb.ToString());
                            return false;
                        }
                        else
                        {
                            foreach (DataTable table in reader.AsDataSet().Tables)
                            {
                                // 테이블 이름이 !이면 읽지 않는다.
                                if (table.TableName[0].Equals('!') ||
                                    table.TableName[0].Equals('_'))
                                    continue;

                                // 테이블 이름에 특수문자가 있으면 읽지 않는다.
                                // 테이블 이름의 첫 글자가 #이나 @인 경우
                                // 서버만 포함, 클라만 포함이라는 조건을 사용하기 때문에 예외
                                if (!table.TableName.StartsWith("#") &&
                                    !table.TableName.StartsWith("@"))
                                {
                                    if (Regex.IsMatch(table.TableName, @"[^a-zA-Z]"))
                                        continue;
                                }

                                if (allExcelDatas.ContainsKey(table.TableName))
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("1) 동일한 Sheet가 있습니다.");
                                    sb.AppendLine("데이터는 Sheet 기준입니다. Sheet명을 다르게 해주세요.");
                                    sb.AppendLine("(Sheet명에 !가 붙으면 읽지 않습니다.)");
                                    sb.AppendLine($"현재 Excel : {fullPath}");
                                    sb.AppendLine($"현재 Sheet : {table.TableName}");
                                    sb.Append($"겹치는 Excel : {checkExcelNames[table.TableName]}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }

                                allExcelDatas.Add(table.TableName, new Dictionary<int, Dictionary<int, ColData>>());
                                allExcelDatas[table.TableName] = new Dictionary<int, Dictionary<int, ColData>>();
                                checkExcelNames.Add(table.TableName, fullPath);

                                for (int row = (int)RowType.Value; row < table.Rows.Count; row++)
                                {
                                    // 종속성 검사를 위해 ENUM_ID를 Key로 하여 Row 한 줄을 담는다.
                                    string enumId = table.Rows[row][(int)ColType.Enum_Id].ToString();

                                    // ENUM_ID가 없으면 마지막 줄이라고 판단한다.
                                    if (string.IsNullOrEmpty(enumId))
                                        break;

                                    //foreach (char c in enumId)
                                    //{
                                    //    if (!char.IsUpper(c) && c != '_')
                                    //    {
                                    //        // ENUM_ID에 유효하지 않은 문자가 있습니다.
                                    //        // ENUM_ID는 알파벳 대문자와 언더바(_)만 사용 가능합니다.
                                    //        // 현재 Excel : {0}
                                    //        // 현재 Sheet : {0}
                                    //        // 현재 ENUM_ID : {0}
                                    //        return false;
                                    //    }
                                    //}

                                    allExcelDatas[table.TableName].Add(row, new Dictionary<int, ColData>());
                                    allExcelDatas[table.TableName][row] = new Dictionary<int, ColData>();

                                    for (int col = (int)ColType.Id; col < table.Columns.Count; col++)
                                    {
                                        string valueInclude = table.Rows[(int)RowType.Include][col].ToString();
                                        string valueType = table.Rows[(int)RowType.Type][col].ToString();
                                        string valueName = table.Rows[(int)RowType.Name][col].ToString();

                                        if (string.IsNullOrEmpty(valueInclude) ||
                                            string.IsNullOrEmpty(valueType) ||
                                            string.IsNullOrEmpty(valueName))
                                        {
                                            // 하나라도 비어 있다면 col의 끝으로 판단한다.
                                            break;
                                        }

                                        // 주석 타입으면 다음으로 넘어간다.
                                        if (valueType.Contains("noti"))
                                            continue;

                                        // 지정 가능한 타입인지 검사한다.
                                        bool isIncludeType = false;
                                        foreach (var includeType in includeTypes)
                                        {
                                            if (valueType.Contains(includeType))
                                            {
                                                isIncludeType = true;
                                                break;
                                            }
                                        }

                                        if (!isIncludeType)
                                        {
                                            StringBuilder sb = new StringBuilder();
                                            sb.AppendLine("지정 불가능한 타입입니다.");
                                            sb.AppendLine("데이터는 Sheet 기준입니다. Sheet명을 다르게 해주세요.");
                                            sb.AppendLine("(Sheet명에 !가 붙으면 읽지 않습니다.)");
                                            sb.AppendLine($"현재 Excel : {fullPath}");
                                            sb.AppendLine($"현재 Sheet : {table.TableName}");
                                            sb.AppendLine($"현재 Row : {row + 1}");
                                            sb.AppendLine($"현재 Col : {NumberToAlphabet(col)}");
                                            sb.Append($"현재 Type : {valueType}");
                                            ShowMessage(sb.ToString());
                                            return false;
                                        }

                                        string value = table.Rows[row][col].ToString().Replace("\n", "");

                                        // 같은 이름의 Col이 이미 있는 경우 ([][]는 안되고 []만 가능하다)
                                        bool isAlready = false;
                                        foreach (var data in allExcelDatas[table.TableName][row])
                                        {
                                            if (data.Value.name == valueName)
                                            {
                                                isAlready = true;
                                                string[] splitArray = value.Split(',');
                                                data.Value.value[data.Value.value.Count - 1].AddRange(splitArray);
                                                data.Value.value[data.Value.value.Count - 1].RemoveAll(match => string.IsNullOrEmpty(match));
                                                break;
                                            }
                                        }

                                        if (isAlready)
                                            continue;

                                        ColData colData = new ColData
                                        {
                                            include = valueInclude,
                                            name = valueName
                                        };

                                        if (valueType.Contains(":"))
                                        {
                                            // 지정 타입이 있는 경우 (enum, class 등)
                                            string[] splitTypes = valueType.Split(':');
                                            string typeNames = splitTypes[1];
                                            string[] splitTypeNames = typeNames.Split(',');

                                            ColData.TypeData typeData = new ColData.TypeData
                                            {
                                                name = splitTypes[0],
                                                value = new List<string>()
                                            };
                                            for (int i = 0; i < splitTypeNames.Length; ++i)
                                            {
                                                if (string.IsNullOrEmpty(splitTypeNames[i]))
                                                    continue;

                                                typeData.value.Add(splitTypeNames[i]);
                                            }
                                            colData.type = typeData;
                                        }
                                        else
                                        {
                                            ColData.TypeData typeData = new ColData.TypeData
                                            {
                                                name = valueType,
                                                value = new List<string>()
                                            };
                                            colData.type = typeData;
                                        }

                                        if (value.Contains(';'))
                                        {
                                            // 다차원 배열
                                            string[] splitArrays = value.Split(';');

                                            colData.value = new List<List<string>>();

                                            for (int i = 0; i < splitArrays.Length; ++i)
                                            {
                                                string[] splitArray = splitArrays[i].Split(',');
                                                colData.value.Add(new List<string>());
                                                colData.value[colData.value.Count - 1].AddRange(splitArray);
                                                colData.value[colData.value.Count - 1].RemoveAll(match => string.IsNullOrEmpty(match));
                                            }
                                        }
                                        else
                                        {
                                            // 배열
                                            string[] splitArray = value.Split(',');

                                            colData.value = new List<List<string>>
                                        {
                                            new List<string>()
                                        };
                                            colData.value[0].AddRange(splitArray);
                                            colData.value[0].RemoveAll(match => string.IsNullOrEmpty(match));
                                        }

                                        allExcelDatas[table.TableName][row].Add(col, colData);
                                    }

                                    if (allExcelEnumIdDatas.ContainsKey(enumId))
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.AppendLine("ENUM_ID가 중복됩니다.");
                                        sb.AppendLine($"현재 Excel : {fullPath}");
                                        sb.AppendLine($"현재 Sheet : {table.TableName}");
                                        sb.AppendLine($"현재 Row : {row + 1}");
                                        sb.AppendLine($"현재 ENUM_ID : {enumId}");
                                        sb.Append($"겹치는 Excel : {checkExcelEnumIds[table.TableName]}");
                                        await ShowMessage(sb.ToString());
                                        return false;
                                    }

                                    allExcelEnumIdDatas.Add(enumId, allExcelDatas[table.TableName][row]);
                                    checkExcelEnumIds.Add(enumId, fullPath);
                                }

                                if (dregDropExcelDatas.ContainsKey(table.TableName))
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("2) 동일한 Sheet가 있습니다.");
                                    sb.AppendLine("데이터는 Sheet 기준입니다. Sheet명을 다르게 해주세요.");
                                    sb.AppendLine("(Sheet명에 !가 붙으면 읽지 않습니다.)");
                                    sb.AppendLine($"현재 Excel : {fullPath}");
                                    sb.AppendLine($"현재 Sheet : {table.TableName}");
                                    sb.Append($"겹치는 Excel : {checkExcelNames[table.TableName]}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }

                                dregDropExcelDatas.Add(table.TableName, allExcelDatas[table.TableName]);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Excel을 읽어오지 못 했습니다.");
            sb.AppendLine("엑셀이 켜져 있거나 손상 되었는지 확인 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }
        return true;
    }

    /// <summary>
    /// 모든 엑셀의 종속성을 검사한다.
    /// </summary>
    public async UniTask<bool> CheckingDependanceExcel()
    {
        await ShowMessage("모든 엑셀의 종속성을 검사 중입니다.");

        foreach (var excel in allExcelDatas)
        {
            foreach (var row in excel.Value)
            {
                // Live가 아닌 Row는 무시한다.
                if (!allExcelDatas[excel.Key][row.Key][(int)ColType.State].value[0][0].Equals("Live"))
                    continue;

                foreach (var col in row.Value)
                {
                    ColData colData = col.Value;

                    if (colData.type.name.Contains("enum:") ||
                        colData.type.name.Contains("enum[]:"))
                    {
                        for (int i = 0; i < colData.value.Count; ++i)
                        {
                            for (int j = 0; j < colData.value[i].Count; ++j)
                            {
                                bool isContains = false;
                                for (int k = 0; k < colData.type.value.Count; ++k)
                                {
                                    if (commonEnum[colData.type.value[k]].ContainsKey(colData.value[i][j]))
                                    {
                                        isContains = true;
                                        break;
                                    }
                                }

                                if (!isContains)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("참조 가능한 CommonEnum을 찾을 수 없습니다.");
                                    sb.AppendLine($"현재 Excel : {excel.Key}");
                                    sb.AppendLine($"현재 Row : {row.Key + 1}");
                                    sb.AppendLine($"현재 Col : {NumberToAlphabet(col.Key)}");
                                    sb.AppendLine($"현재 Value : {colData.value[i][j]}");

                                    StringBuilder sb2 = new StringBuilder();
                                    for (int k = 0; k < colData.type.value.Count; ++k)
                                    {
                                        if (sb2.Length > 0)
                                            sb2.Append(',');

                                        sb2.Append(colData.type.value[k]);
                                    }

                                    sb.Append($"찾으려는 CommonEnum : {sb2.ToString()}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }
                            }
                        }
                    }
                    else if (colData.type.name.Contains("class"))
                    {
                        for (int i = 0; i < colData.value.Count; ++i)
                        {
                            for (int j = 0; j < colData.value[i].Count; ++j)
                            {
                                // NULL 또는 EMPTY면 다음으로 넘어간다.
                                if (colData.value[i][j].Equals("NULL") ||
                                    colData.value[i][j].Equals("EMPTY"))
                                    continue;

                                if (!allExcelEnumIdDatas.ContainsKey(colData.value[i][j]))
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("참조 가능한 ENUM_ID를 찾을 수 없습니다.");
                                    sb.AppendLine($"현재 Excel : {excel.Key}");
                                    sb.AppendLine($"현재 Row : {row.Key + 1}");
                                    sb.AppendLine($"현재 Col : {NumberToAlphabet(col.Key)}");
                                    sb.AppendLine($"현재 Value : {colData.value[i][j]}");

                                    StringBuilder sb2 = new StringBuilder();
                                    for (int k = 0; k < colData.type.value.Count; ++k)
                                    {
                                        if (sb2.Length > 0)
                                            sb2.Append(',');

                                        sb2.Append(colData.type.value[k]);
                                    }

                                    sb.Append($"찾으려는 CommonEnum : {sb2.ToString()}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }

                                if (!allExcelEnumIdDatas[colData.value[i][j]][(int)ColType.State].value[0][0].Equals("Live"))
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("참조중인 데이터가 Live가 아닙니다.");
                                    sb.AppendLine($"현재 Excel : {excel.Key}");
                                    sb.AppendLine($"현재 Row : {row.Key + 1}");
                                    sb.AppendLine($"현재 Col : {NumberToAlphabet(col.Key)}");
                                    sb.AppendLine($"현재 Value : {colData.value[i][j]}");

                                    StringBuilder sb2 = new StringBuilder();
                                    for (int k = 0; k < colData.type.value.Count; ++k)
                                    {
                                        if (sb2.Length > 0)
                                            sb2.Append(',');

                                        sb2.Append(colData.type.value[k]);
                                    }

                                    sb.Append($"찾으려는 CommonEnum : {sb2.ToString()}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 데이터를 json으로 파싱한다.
    /// </summary>
    public async UniTask<bool> JsonParse()
    {
        try
        {
            await ShowMessage("데이터를 json으로 변환 중입니다.");

            //[엑셀이름][row에 해당하는 정보들]
            Dictionary<string, List<Dictionary<string, object>>> clientJsonDic = new Dictionary<string, List<Dictionary<string, object>>>();
            Dictionary<string, List<Dictionary<string, object>>> serverJsonDic = new Dictionary<string, List<Dictionary<string, object>>>();

            foreach (var excel in dregDropExcelDatas)
            {
                if (excel.Key.Equals("Localization"))
                {
                    // Localization의 경우 한 Sheet 안에서 언어별로 Json이 분리 되어야 하기 때문에 예외처리를 한다.

                    foreach (var row in excel.Value)
                    {
                        // Live가 아닌 Row는 무시한다.
                        if (!dregDropExcelDatas[excel.Key][row.Key][(int)ColType.State].value[0][0].Equals("Live"))
                            continue;

                        int index = 0;
                        Dictionary<string, string> localizationKey = new Dictionary<string, string>();
                        foreach (var col in row.Value)
                        {
                            if (index <= (int)ColType.State)
                            {
                                index++;
                                continue;
                            }

                            ColData colData = col.Value;
                            Dictionary<string, object> keyValue = new Dictionary<string, object>
                            {
                                { "VALUE", colData.value[0][0] }
                            };

                            // Localization_kor, Localization_eng 등의 이름을 만들고 가지고 있는다.
                            // (매번 string을 할당하면 느리기 때문)
                            if (!localizationKey.ContainsKey(colData.name))
                                localizationKey.Add(colData.name, $"Localization_{colData.name.ToLower()}");

                            if (!clientJsonDic.ContainsKey(localizationKey[colData.name]))
                                clientJsonDic.Add(localizationKey[colData.name], new List<Dictionary<string, object>>());

                            // col의 name이 KOR, ENG 형식으로 되어 있으나
                            // json으로 파싱할 때에는 Localization_kor, Localization_eng 등으로 나누어진다.
                            clientJsonDic[localizationKey[colData.name]].Add(keyValue);
                        }
                    }
                }
                else
                {
                    bool isClientOnly = excel.Key.Contains("@");
                    bool isServerOnly = excel.Key.Contains("#");

                    if (!isServerOnly)
                        clientJsonDic.Add(excel.Key, new List<Dictionary<string, object>>());

                    if (!isClientOnly)
                        serverJsonDic.Add(excel.Key, new List<Dictionary<string, object>>());

                    foreach (var row in excel.Value)
                    {
                        // Live가 아닌 Row는 무시한다.
                        if (!dregDropExcelDatas[excel.Key][row.Key][(int)ColType.State].value[0][0].Equals("Live"))
                            continue;

                        Dictionary<string, object> clientKeyValue = new Dictionary<string, object>();
                        Dictionary<string, object> serverKeyValue = new Dictionary<string, object>();

                        foreach (var col in row.Value)
                        {
                            ColData colData = col.Value;

                            if (colData.name.Equals("ENUM_ID"))
                            {
                                // ENUM_ID일 때 아래 지정한 Sheet만 포함 시킨다.
                                if (inclueENUM_IDExcels.Contains(excel.Key))
                                    clientKeyValue.Add(colData.name, colData.value[0][0]);

                                serverKeyValue.Add(colData.name, colData.value[0][0]);
                                continue;
                            }
                            else if (colData.type.name.Contains("enum") ||
                                     colData.type.name.Contains("enum[]"))
                            {
                                bool isArray = colData.type.name.Contains("enum[]");

                                if (isArray)
                                {
                                    List<int> list = new List<int>();

                                    for (int i = 0; i < colData.value.Count; ++i)
                                    {
                                        for (int j = 0; j < colData.value[i].Count; ++j)
                                        {
                                            for (int k = 0; k < colData.type.value.Count; ++k)
                                            {
                                                if (commonEnum[colData.type.value[k]].ContainsKey(colData.value[i][j]))
                                                {
                                                    list.Add(commonEnum[colData.type.value[k]][colData.value[i][j]]);
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (!colData.include.Equals("S"))
                                        clientKeyValue.Add(colData.name, list);
                                    if (!colData.include.Equals("C"))
                                        serverKeyValue.Add(colData.name, list);
                                }
                                else
                                {
                                    for (int i = 0; i < colData.value.Count; ++i)
                                    {
                                        for (int j = 0; j < colData.value[i].Count; ++j)
                                        {
                                            for (int k = 0; k < colData.type.value.Count; ++k)
                                            {
                                                if (commonEnum[colData.type.value[k]].ContainsKey(colData.value[i][j]))
                                                {
                                                    if (!colData.include.Equals("S"))
                                                        clientKeyValue.Add(colData.name, commonEnum[colData.type.value[k]][colData.value[i][j]]);
                                                    if (!colData.include.Equals("C"))
                                                        serverKeyValue.Add(colData.name, commonEnum[colData.type.value[k]][colData.value[i][j]]);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else if (colData.type.name.Contains("class"))
                            {
                                bool isMultiArray = colData.type.name.Contains("class[][]");
                                bool isArray = !isMultiArray && colData.type.name.Contains("class[]");

                                if (isMultiArray)
                                {
                                    List<List<int>> list = new List<List<int>>();

                                    for (int i = 0; i < colData.value.Count; ++i)
                                    {
                                        list.Add(new List<int>());

                                        for (int j = 0; j < colData.value[i].Count; ++j)
                                        {
                                            // EMPTY는 빈 배열이다.
                                            if (colData.value[i][j].Equals("EMPTY"))
                                                break;

                                            // NULL은 참조가 없는 것으로 0이다.
                                            if (colData.value[i][j].Equals("NULL"))
                                            {
                                                bool isEndArray = true;
                                                for (int k = j; k < colData.value[i].Count; ++k)
                                                {
                                                    if (colData.value[i][k].Equals("NULL"))
                                                        continue;

                                                    isEndArray = false;
                                                    break;
                                                }

                                                // j부터 마지막 Length까지 전부 NULL이면
                                                // 그 뒤로는 없는 정보로 판단해서 배열의 끝으로 본다.
                                                // Balance 데이터는 NULL,NULL . . . . 쭉 이어져 있다.
                                                if (isEndArray)
                                                    break;

                                                list[i].Add(0);
                                                continue;
                                            }

                                            Debug.Log(colData.value.Count);
                                            Debug.Log(colData.value[i].Count);
                                            Debug.Log(allExcelEnumIdDatas[colData.value[i][j]][(int)ColType.Id]);
                                            Debug.Log(allExcelEnumIdDatas[colData.value[i][j]][(int)ColType.Id].value[0][0]);

                                            if (i >= list.Count)
                                            {
                                                Debug.Log("ERROR");
                                            }

                                            list[i].Add(int.Parse(allExcelEnumIdDatas[colData.value[i][j]][(int)ColType.Id].value[0][0]));
                                        }
                                    }
                                    if (!colData.include.Equals("S"))
                                        clientKeyValue.Add(colData.name, list);
                                    if (!colData.include.Equals("C"))
                                        serverKeyValue.Add(colData.name, list);
                                }
                                else if (isArray)
                                {
                                    List<int> list = new List<int>();

                                    if (colData.value.Count == 1 &&
                                        colData.value[0].Count == 1)
                                    {
                                        // EMPTY는 빈 배열이다.
                                        if (colData.value[0][0].Equals("EMPTY"))
                                            continue;

                                        if (colData.value[0][0].Equals("NULL"))
                                        {
                                            list.Add(0);

                                            if (!colData.include.Equals("S"))
                                                clientKeyValue.Add(colData.name, list);
                                            if (!colData.include.Equals("C"))
                                                serverKeyValue.Add(colData.name, list);

                                            continue;
                                        }
                                    }

                                    for (int i = 0; i < colData.value.Count; ++i)
                                    {
                                        for (int j = 0; j < colData.value[i].Count; ++j)
                                        {
                                            // NULL은 참조가 없는 것으로 0이다.
                                            if (colData.value[i][j].Equals("NULL"))
                                            {
                                                bool isEndArray = true;
                                                for (int k = j; k < colData.value[i].Count; ++k)
                                                {
                                                    if (colData.value[i][k].Equals("NULL"))
                                                        continue;

                                                    isEndArray = false;
                                                    break;
                                                }

                                                // j부터 마지막 Length까지 전부 NULL이면
                                                // 그 뒤로는 없는 정보로 판단해서 배열의 끝으로 본다.
                                                // Balance 데이터는 NULL,NULL . . . . 쭉 이어져 있다.
                                                if (isEndArray)
                                                    break;

                                                list.Add(0);
                                                continue;
                                            }
                                            else if (colData.value[i][j].Equals("EMPTY"))
                                            {
                                                break;
                                            }

                                            list.Add(int.Parse(allExcelEnumIdDatas[colData.value[i][j]][(int)ColType.Id].value[0][0]));
                                        }
                                    }

                                    if (!colData.include.Equals("S"))
                                        clientKeyValue.Add(colData.name, list);
                                    if (!colData.include.Equals("C"))
                                        serverKeyValue.Add(colData.name, list);
                                }
                                else
                                {
                                    // NULL은 참조가 없는 것으로 0이다.
                                    if (colData.value[0][0].Equals("NULL"))
                                    {
                                        if (!colData.include.Equals("S"))
                                            clientKeyValue.Add(colData.name, 0);
                                        if (!colData.include.Equals("C"))
                                            serverKeyValue.Add(colData.name, 0);
                                    }
                                    else
                                    {
                                        if (!colData.include.Equals("S"))
                                            clientKeyValue.Add(colData.name, int.Parse(allExcelEnumIdDatas[colData.value[0][0]][(int)ColType.Id].value[0][0]));
                                        if (!colData.include.Equals("C"))
                                            serverKeyValue.Add(colData.name, int.Parse(allExcelEnumIdDatas[colData.value[0][0]][(int)ColType.Id].value[0][0]));
                                    }
                                }
                            }
                            else if (colData.type.name.Contains("[]"))
                            {
                                List<object> list = new List<object>();

                                if (colData.type.name.Contains("string"))
                                {
                                    for (int i = 0; i < colData.value.Count; ++i)
                                    {
                                        for (int j = 0; j < colData.value[i].Count; ++j)
                                        {
                                            if (colData.value[i][j].Equals("NULL"))
                                                list.Add(string.Empty); // list.Add("NULL"); 원래는 NULL인데 이거 확인 필요
                                            else if (colData.value[i][j].Equals("EMPTY"))
                                                continue;
                                            else
                                                list.Add(colData.value[i][j]);
                                        }
                                    }
                                }
                                else
                                {
                                    if (colData.type.name.Contains("int"))
                                    {
                                        for (int i = 0; i < colData.value.Count; ++i)
                                        {
                                            for (int j = 0; j < colData.value[i].Count; ++j)
                                            {
                                                if (colData.value[i][j].Contains("NULL"))
                                                    break;

                                                list.Add(int.Parse(colData.value[i][j]));
                                            }
                                        }
                                    }
                                    else if (colData.type.name.Contains("float"))
                                    {
                                        for (int i = 0; i < colData.value.Count; ++i)
                                        {
                                            for (int j = 0; j < colData.value[i].Count; ++j)
                                            {
                                                if (colData.value[i][j].Contains("NULL"))
                                                    break;

                                                list.Add(float.Parse(colData.value[i][j]));
                                            }
                                        }
                                    }
                                    else if (colData.type.name.Contains("decimal"))
                                    {
                                        for (int i = 0; i < colData.value.Count; ++i)
                                        {
                                            for (int j = 0; j < colData.value[i].Count; ++j)
                                            {
                                                if (colData.value[i][j].Contains("NULL"))
                                                    break;

                                                list.Add(decimal.Parse(colData.value[i][j]));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.AppendLine("1) 잘못된 타입 값입니다.");
                                        sb.AppendLine($"현재 Excel : {excel.Key}");
                                        sb.AppendLine($"현재 Row : {row.Key + 1}");
                                        sb.AppendLine($"현재 Col : {NumberToAlphabet(col.Key)}");
                                        sb.AppendLine($"현재 Type : {colData.type.name}");
                                        await ShowMessage(sb.ToString());
                                        return false;
                                    }
                                }

                                if (!colData.include.Equals("S"))
                                    clientKeyValue.Add(colData.name, list);
                                if (!colData.include.Equals("C"))
                                    serverKeyValue.Add(colData.name, list);
                            }
                            else
                            {
                                object value = null;

                                if (colData.type.name.Equals("int"))
                                {
                                    value = int.Parse(colData.value[0][0]);
                                }
                                else if (colData.type.name.Equals("float"))
                                {
                                    value = float.Parse(colData.value[0][0]);
                                }
                                else if (colData.type.name.Equals("decimal"))
                                {
                                    value = decimal.Parse(colData.value[0][0]);
                                }
                                else if (colData.type.name.Equals("string"))
                                {
                                    if (colData.value[0] == null || colData.value[0].Count == 0)
                                    {
                                        value = string.Empty;
                                    }
                                    else
                                    {
                                        value = colData.value[0][0];

                                        if (colData.value[0][0].Equals("NULL"))
                                            value = string.Empty;
                                    }
                                }
                                else
                                {
                                    // 잘못된 타입 값입니다.
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("2) 잘못된 타입 값입니다.");
                                    sb.AppendLine($"현재 Excel : {excel.Key}");
                                    sb.AppendLine($"현재 Row : {row.Key + 1}");
                                    sb.AppendLine($"현재 Col : {NumberToAlphabet(col.Key)}");
                                    sb.AppendLine($"현재 Type : {colData.type.name}");
                                    await ShowMessage(sb.ToString());
                                    return false;
                                }

                                if (!colData.include.Equals("S"))
                                    clientKeyValue.Add(colData.name, value);
                                if (!colData.include.Equals("C"))
                                    serverKeyValue.Add(colData.name, value);
                            }
                        }

                        if (!isServerOnly)
                            clientJsonDic[excel.Key].Add(clientKeyValue);

                        if (!isServerOnly)
                            serverJsonDic[excel.Key].Add(serverKeyValue);
                    }
                }
            }

            // 클라이언트 데이터
            foreach (var json in clientJsonDic)
            {
                string jsonfile = JsonConvert.SerializeObject(json.Value);//, Formatting.Indented);
                System.IO.File.WriteAllText($"{clientJsonPath}/{json.Key}.json", jsonfile.Trim());
            }

            // 서버 데이터
            foreach (var json in serverJsonDic)
            {
                string jsonfile = JsonConvert.SerializeObject(json.Value);//, Formatting.Indented);
                System.IO.File.WriteAllText($"{serverJsonPath}/{json.Key}.json", jsonfile.Trim());
            }

            // 클라우드 데이터
            StringBuilder cloudValueData = new StringBuilder();
            StringBuilder cloudPropertyData = new StringBuilder();
            foreach (var json in serverJsonDic)
            {
                string jsonfile = JsonConvert.SerializeObject(json.Value);
                jsonfile = jsonfile.Replace("\"", "\"\"");

                if (cloudValueData.Length > 0)
                    cloudValueData.Append("\r\t");

                if (cloudPropertyData.Length > 0)
                    cloudPropertyData.Append("\r\t");

                cloudPropertyData.Append($"public static readonly List<{json.Key}TableData> {json.Key}TableData = JsonConvert.DeserializeObject<List<{json.Key}TableData>>({json.Key});");
                cloudValueData.Append($"public const string {json.Key} = @\"{jsonfile}\";");
            }

            string tableTemplateCode = File.ReadAllText($"{clientTempletPath}CloudTable.txt");
            tableTemplateCode = tableTemplateCode.Replace("#PROPERTYS#", cloudPropertyData.ToString());
            tableTemplateCode = tableTemplateCode.Replace("#VALUES#", cloudValueData.ToString());

            System.IO.File.WriteAllText($"{cloudJsonPath}/CloudTable.cs", tableTemplateCode);

            // 클라 DataVersion json 파일
            string clientVersionPath = $"{clientVersionJsonPath}/DataVersion.json";
            if (!System.IO.File.Exists(clientVersionPath))
            {
                string clientDataVersionJsonfile = JsonConvert.SerializeObject(new DataVersion
                {
                    dataFileVersion = new List<DataFileVersion>()
                });
                System.IO.File.WriteAllText(clientVersionPath, clientDataVersionJsonfile.Trim());
            }
            string clientVersionJson = System.IO.File.ReadAllText(clientVersionPath);
            DataVersion clientDataVersion = JsonConvert.DeserializeObject<DataVersion>(clientVersionJson);
            clientDataVersion.version = GetHashCode();
            foreach (var json in clientJsonDic)
            {
                var dataFile = clientDataVersion.dataFileVersion.Find(match => match.fileName == json.Key);
                if (dataFile.IsNull)
                {
                    clientDataVersion.dataFileVersion.Add(new DataFileVersion()
                    {
                        fileName = json.Key,
                        version = 0,
                    });

                    continue;
                }

                dataFile.version++;
            }
            System.IO.File.WriteAllText($"{clientVersionJsonPath}/DataVersion.json", JsonConvert.SerializeObject(clientDataVersion));

            // 서버 DataVersion json 파일
            string serverVersionPath = $"{serverVersionJsonPath}/DataVersion.json";
            if (!System.IO.File.Exists(serverVersionPath))
            {
                string serverDataVersionJsonfile = JsonConvert.SerializeObject(new DataVersion
                {
                    dataFileVersion = new List<DataFileVersion>()
                });
                System.IO.File.WriteAllText(serverVersionPath, serverDataVersionJsonfile);
            }
            string serverVersionJson = System.IO.File.ReadAllText(serverVersionPath);
            DataVersion serverDataVersion = JsonConvert.DeserializeObject<DataVersion>(serverVersionJson);
            serverDataVersion.version = GetHashCode();
            foreach (var json in serverJsonDic)
            {
                var dataFile = serverDataVersion.dataFileVersion.Find(match => match.fileName == json.Key);
                if (dataFile.IsNull)
                {
                    serverDataVersion.dataFileVersion.Add(new DataFileVersion()
                    {
                        fileName = json.Key,
                        version = 0,
                    });

                    continue;
                }

                dataFile.version++;
            }
            System.IO.File.WriteAllText($"{serverVersionJsonPath}/DataVersion.json", JsonConvert.SerializeObject(serverDataVersion));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("제이슨 파일을 생성하지 못 했습니다.");
            sb.AppendLine("해당 에러는 클라이언트팀에 문의 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }
    }

    /// <summary>
    /// 클라이언트 코드를 생성한다.
    /// </summary>
    public async UniTask<bool> ClientScriptGenerator()
    {
        if (!await LocalizationDataGenerator())
            return false;

        if (!await DataGenerator())
            return false;

        if (!await EnumDataGenerator())
            return false;

        if (!await UtilityGenerator())
            return false;

        return true;
    }

    private async UniTask<bool> LocalizationDataGenerator()
    {
        try
        {
            await ShowMessage("로컬라이징 데이터를 클라이언트 코드로 변환 중입니다.");

            StringBuilder valuesBuilder = new StringBuilder();
            StringBuilder loadTableBuilder = new StringBuilder();
            StringBuilder getString1ValuesBuilder = new StringBuilder();
            StringBuilder getString2ValuesBuilder = new StringBuilder();

            List<string> languageType = new List<string>();

            foreach (var excel in dregDropExcelDatas)
            {
                bool isServerOnly = excel.Key.Contains("#");

                if (isServerOnly)
                    continue;

                if (!excel.Key.Equals("Localization"))
                    continue;

                string tableTemplateCode = File.ReadAllText($"{clientTempletPath}Localization.txt");
                string tableDataTemplateCode = File.ReadAllText($"{clientTempletPath}LocalizationTable.txt");

                foreach (var row in excel.Value)
                {
                    foreach (var col in row.Value)
                    {
                        var colData = col.Value;
                        string type = colData.type.name;

                        // 서버만 포함인 경우 다음으로 넘어간다.
                        if (colData.include.Equals("S"))
                            continue;

                        if (!colData.type.name.Equals("string"))
                            continue;

                        string language = colData.name.ToLower();
                        StringBuilder sb = new StringBuilder();
                        sb.Append(language[0].ToString().ToUpper());

                        for (int i = 1; i < language.Length; ++i)
                            sb.Append(language[i]);

                        string caseLanguage = sb.ToString();

                        if (valuesBuilder.Length > 0)
                            valuesBuilder.Append($",\r\t\t\t");
                        valuesBuilder.Append(caseLanguage);

                        loadTableBuilder.Append($"case Localization.LanguageType.{caseLanguage}:\r\t\t\t\t\t{{\r\t\t\t\t\t\ttable = new Localization_{language}Table();");
                        loadTableBuilder.Append($"\r\t\t\t\t\t\ttable.LoadTable(UtilModel.Resources.LoadTextAsset(json + table.FileName));");
                        loadTableBuilder.Append($"\r\t\t\t\t\t\tbreak;\r\t\t\t\t\t}}\r\r\t\t\t\t\t");

                        getString1ValuesBuilder.Append($"case Localization.LanguageType.{caseLanguage}:\r\t\t\t\t{{\r\t\t\t\t\t");
                        getString1ValuesBuilder.Append($"Localization_{language}Table localization = ((Localization_{language}Table)table;");
                        getString1ValuesBuilder.Append($"\r\t\t\t\t\tif (localization == null)\r\t\t\t\t\t\treturn string.Empty;");
                        getString1ValuesBuilder.Append($"\r\r\t\t\t\t\treturn localization.GetDataByID(id).GetVALUE();\r\t\t\t\t}}\r\r\t\t\t\t");

                        getString2ValuesBuilder.Append($"case Localization.LanguageType.{caseLanguage}:\r\t\t\t\t{{\r\t\t\t\t\t");
                        getString2ValuesBuilder.Append($"Localization_{language}Table localization = ((Localization_{language}Table)table;");
                        getString2ValuesBuilder.Append($"\r\t\t\t\t\tif (localization == null)\r\t\t\t\t\t\treturn string.Empty;");
                        getString2ValuesBuilder.Append($"\r\r\t\t\t\t\treturn localization.GetDataByEnumId(enumId).GetVALUE();\r\t\t\t\t}}\r\r\t\t\t\t");
                    }

                    break;
                }

                System.Action<string, string> generate = (templateCode, fileName) =>
                {
                    // 변수
                    templateCode = templateCode.Replace("#VALUES#", valuesBuilder.ToString());
                    templateCode = templateCode.Replace("#VALUES1#", loadTableBuilder.ToString());
                    templateCode = templateCode.Replace("#VALUES2#", getString1ValuesBuilder.ToString());
                    templateCode = templateCode.Replace("#VALUES3#", getString2ValuesBuilder.ToString());

                    File.WriteAllText($"{clientTempletResultPath}{fileName}", templateCode);
                };

                generate(tableTemplateCode, $"Localization.cs");
                generate(tableDataTemplateCode, $"LocalizationTable.cs");

                break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("로컬라이징 데이터를 클라이언트 코드로 변환하지 못 했습니다.");
            sb.AppendLine("해당 에러는 클라이언트팀에 문의 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }

        return true;
    }

    private async UniTask<bool> DataGenerator()
    {
        await ShowMessage("데이터를 클라이언트 코드로 변환 중입니다.");

        StringBuilder publicValuesBuilder = new StringBuilder();
        StringBuilder pravteValuesBuilder = new StringBuilder();
        StringBuilder propertyValues = new StringBuilder();
        StringBuilder deserializeValues = new StringBuilder();

        try
        {
            foreach (var excel in dregDropExcelDatas)
            {
                if (excel.Key.Contains("Localization"))
                    continue;

                string tableTemplateCode = File.ReadAllText($"{clientTempletPath}Table.txt");
                string tableDataTemplateCode = File.ReadAllText($"{clientTempletPath}TableData.txt");

                string templateCode = string.Empty;

                foreach (var row in excel.Value)
                {
                    publicValuesBuilder.Clear();

                    foreach (var col in row.Value)
                    {
                        var colData = col.Value;

                        string type = colData.type.name;

                        // 서버만 포함인 경우 다음으로 넘어간다.
                        if (colData.include.Equals("S"))
                            continue;

                        if (string.IsNullOrEmpty(colData.type.name))
                            continue;

                        if (colData.name.Contains("ENUM_ID"))
                        {
                            continue;
                        }
                        else if (colData.type.name.Contains("enum"))
                        {
                            if (colData.name.Contains("STATE"))
                                continue;

                            // enum는 int형으로
                            type = "int";
                        }
                        else if (colData.type.name.Contains("class"))
                        {
                            // class는 int형으로
                            type = "int";
                        }
                        else if (colData.type.name.Contains("int"))
                        {
                            if (colData.name.Equals("ID"))
                                continue;
                        }

                        if (publicValuesBuilder.Length > 0)
                            publicValuesBuilder.Append("\r\t");

                        if (colData.type.name.Contains("[]"))
                            publicValuesBuilder.Append($"public {type} {colData.name};");
                        else
                            publicValuesBuilder.Append($"public {type} {colData.name};");
                    }

                    break;
                }

                System.Action<string, string, string> generate = (templateCode, fileName, result) =>
                {
                    // 스크립트 이름
                    templateCode = templateCode.Replace("#SCRIPTNAME#", excel.Key);

                    // 변수
                    templateCode = templateCode.Replace("#VALUES#", publicValuesBuilder.ToString());

                    File.WriteAllText($"{result}{fileName}", templateCode, Encoding.UTF8);
                };

                generate(tableTemplateCode, $"{excel.Key}Table.cs", clientTempletResultPath);
                generate(tableDataTemplateCode, $"{excel.Key}TableData.cs", clientTempletResultPath);
                generate(tableDataTemplateCode, $"{excel.Key}TableData.cs", cloudTempletResultPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("데이터를 클라이언트 코드로 변환하지 못 했습니다.");
            sb.AppendLine("해당 에러는 클라이언트팀에 문의 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }

        return true;
    }

    private async UniTask<bool> EnumDataGenerator()
    {
        await ShowMessage("데이터를 클라이언트 Define 코드로 변환 중입니다.");

        StringBuilder enumValuesBuilder = new StringBuilder();
        StringBuilder enumEditorValuesBuilder = new StringBuilder();

        try
        {
            foreach (var excel in dregDropExcelDatas)
            {
                if (excel.Key.Contains("Localization"))
                    continue;

                string tableTemplateCode = File.ReadAllText($"{clientTempletPath}/EnumIdDefine.txt");
                string templateCode = string.Empty;

                enumValuesBuilder.Clear();
                enumEditorValuesBuilder.Clear();

                foreach (var row in excel.Value)
                {
                    var colState = row.Value[(int)ColType.State];

                    // 서버만 포함인 경우 다음으로 넘어간다.
                    if (colState.include.Equals("S"))
                        continue;

                    var colId = row.Value[(int)ColType.Id];
                    var colEnumId = row.Value[(int)ColType.Enum_Id];
                    var enumId = colEnumId.value[0][0].Replace('-', '_');

                    if (enumValuesBuilder.Length > 0)
                        enumValuesBuilder.Append($",\r\t\t");
                    enumValuesBuilder.Append($"{enumId} = {colId.value[0][0]}");

                    if (enumEditorValuesBuilder.Length > 0)
                        enumEditorValuesBuilder.Append($",\r\t\t");
                    enumEditorValuesBuilder.Append($"{{ \"{enumId}\", {excel.Key}TableDefine.{enumId} }}");
                }

                System.Action<string, string, string> generate = (templateCode, fileName, result) =>
                {
                    // 스크립트 이름
                    templateCode = templateCode.Replace("#ENUMNAME#", $"{excel.Key}TableDefine");

                    // 변수
                    templateCode = templateCode.Replace("#VALUES#", enumValuesBuilder.ToString());
                    templateCode = templateCode.Replace("#EDITOR_VALUE#", enumEditorValuesBuilder.ToString());

                    File.WriteAllText($"{result}{fileName}", templateCode, Encoding.UTF8);
                };

                generate(tableTemplateCode, $"{excel.Key}TableDefine.cs", clientTempletResultPath);
                generate(tableTemplateCode, $"{excel.Key}TableDefine.cs", cloudTempletResultPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("데이터를 클라이언트 Define 코드로 변환하지 못 했습니다.");
            sb.AppendLine("해당 에러는 클라이언트팀에 문의 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }

        return true;
    }

    private async UniTask<bool> UtilityGenerator()
    {
        await ShowMessage("데이터를 클라이언트 Define 코드로 변환 중입니다.");

        StringBuilder enumValuesBuilder = new StringBuilder();
        StringBuilder utilityValuesBuilder = new StringBuilder();

        try
        {
            string enumTemplateCode = File.ReadAllText($"{clientTempletPath}TableEnum.txt");
            string utilityTemplateCode = File.ReadAllText($"{clientTempletPath}TableUtility.txt");

            System.Action<string, string> generateEnum = (templateCode, fileName) =>
            {
                templateCode = templateCode.Replace("#TABLETYPE#", enumValuesBuilder.ToString());

                File.WriteAllText($"{clientTempletEnumResultPath}{fileName}", templateCode, Encoding.UTF8);
            };

            System.Action<string, string> generateUtility = (templateCode, fileName) =>
            {
                templateCode = templateCode.Replace("#TABLETYPE#", utilityValuesBuilder.ToString());

                File.WriteAllText($"{clientTempletUtilityResultPath}{fileName}", templateCode, Encoding.UTF8);
            };

            foreach (var excel in dregDropExcelDatas)
            {
                if (enumValuesBuilder.Length > 0)
                    enumValuesBuilder.Append(",\r\t");

                enumValuesBuilder.Append(excel.Key);

                if (utilityValuesBuilder.Length > 0)
                    utilityValuesBuilder.Append("\r\t\t\t");

                utilityValuesBuilder.Append($"TableType.{excel.Key} => new {excel.Key}Table(),");
            }

            generateEnum(enumTemplateCode, $"TableEnum.cs");
            generateUtility(utilityTemplateCode, $"TableUtility.cs");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.StackTrace}");
            Debug.LogError($"{e.Message}");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("데이터를 클라이언트 Utility 코드로 변환하지 못 했습니다.");
            sb.AppendLine("해당 에러는 클라이언트팀에 문의 바랍니다.");
            sb.AppendLine($"에러 Stack : {e.StackTrace}");
            sb.Append($"에러 Message : {e.Message}");
            await ShowMessage(sb.ToString());
            return false;
        }

        return true;
    }

    public string NumberToAlphabet(int number)
    {
        string result = "";

        while (number >= 0)
        {
            int remainder = number % 26;
            result = (char)('A' + remainder) + result;
            number = (number / 26) - 1;
        }

        return result;
    }
}
#endif