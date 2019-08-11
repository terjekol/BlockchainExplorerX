using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BlockchainExplorer.Model;
using Info.Blockchain.API.BlockExplorer;
using Info.Blockchain.API.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace BlockchainExplorer.Pages
{
    public class IndexModel : PageModel
    {
        private object _object;
        public string ActionName { get; private set; }
        public PropertyInfo[] SimpleProps { get; private set; }
        public PropertyInfo[] CollectionProps { get; private set; }

        public ICollection Collection { get; private set; }
        public IEnumerable<Search> RecentSearches { get; set; }
        public Search CurrentSearch { get; set; }

        public IEnumerable<MethodInfo> Actions { get; private set; }
        private void CreateActions()
        {
            Actions = typeof(BlockExplorer)
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetParameters().Length <= 1);
        }

        public string ShortenName(string s)
        {
            if (s == null) return s;
            return s.Replace("Get", "").Replace("Async", "");
        }

        public string InputTypeFromCsType(Type t)
        {
            if (t == typeof(DateTime)) return "date";
            if (t == typeof(long)) return "number";
            return "text";
        }


        public async Task OnGet(string actionName, string paramValue, int[] indexes)
        {
            CreateActions();
            if (actionName == null) return;
            ActionName = ShortenName(actionName);
            var action = Actions.SingleOrDefault(a => a.Name.Contains(actionName));
            if (action == null) return;
            CurrentSearch = new Search { ActionName = actionName, ParamValue = paramValue, Indexes = new List<int>(indexes) };
            var obj = await DoAction(paramValue, action);
            Save(obj);
        }


        private async Task<object> DoAction(string paramValue, MethodInfo action)
        {
            var explorer = new BlockExplorer();
            var param = action.GetParameters().FirstOrDefault();
            var paramsObj = param == null ? new object[] { } : new[] { ConvertValue(paramValue, param.ParameterType) };
            var task = action.Invoke(explorer, paramsObj) as Task;
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            var obj = resultProperty.GetValue(task);
            obj = IfCollection(obj);
            obj = await IfLatestBlock(obj, explorer);
            return obj;
        }

        private void Save(object obj)
        {
            var type = obj.GetType();
            var allProps = type.GetProperties();
            SimpleProps = allProps.Where(p => !p.PropertyType.Name.Contains("Collection")).ToArray();
            CollectionProps = allProps.Where(p => p.PropertyType.Name.Contains("Collection")).ToArray();
            _object = obj;
        }

        private object ConvertValue(string value, Type type)
        {
            if (type == typeof(long)) return Convert.ToInt64(value);
            if (type != typeof(DateTime)) return value;
            DateTime.TryParse(value, out var result);
            return result;
        }

        private object IfCollection(object o)
        {
            var collection = o as ICollection;
            if (collection == null) return o;
            Collection = collection;
            var enumerator = collection.GetEnumerator();
            if (CurrentSearch.Indexes.Count > 0)
            {
                var skipCount = CurrentSearch.Indexes[0];
                while (skipCount-- > 0) enumerator.MoveNext();
            }
            enumerator.MoveNext();
            return enumerator.Current;
        }

        private async Task<object> IfLatestBlock(object obj, BlockExplorer explorer)
        {
            var latestBlock = obj as LatestBlock;
            if (latestBlock == null) return obj;
            return await explorer.GetBlockByHashAsync(latestBlock.Hash);
        }

        public object GetCollectionElement(PropertyInfo prop, int collectionIndex)
        {
            var collection = prop.GetValue(_object) as IEnumerable<object>;
            if (collection == null) return null;
            var skipCount = CurrentSearch.GetCollectionNo(collectionIndex) - 1;
            return collection.Skip(skipCount).FirstOrDefault();
        }

        public int GetCollectionCount(PropertyInfo prop, int collectionIndex)
        {
            var collection = prop.GetValue(_object) as IEnumerable<object>;
            return collection?.Count() ?? 0;
        }

        public string GetValue(PropertyInfo prop)
        {
            return prop.GetValue(_object).ToString();
        }

    }
}
