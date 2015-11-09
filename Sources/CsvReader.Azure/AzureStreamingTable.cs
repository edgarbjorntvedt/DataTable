﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;

namespace DataAccess
{
    // Used for reading from an Azure Table.
    // Determine columns based on first row. 
    internal class AzureStreamingTable : DataTable
    {
        public CloudTableClient _tableClient;

        // This is different than the Name property, because Name is just a hint and can be redefined by the user. 
        // Whereas this property really is the azure table name. 
        public string _tableName;

        public string[] _columnNames;

        public override IEnumerable<string> ColumnNames
        {
            get
            {
                InitColumnNames();
                return _columnNames;
            }
        }

        private void InitColumnNames()
        {
            if (_columnNames == null)
            {
                var ctx = _tableClient.GetTableServiceContext();
                ctx.IgnoreMissingProperties = true;
                ctx.Format.UseAtom();
                ctx.ReadingEntity += GenericTableReader.OnReadingEntity;

                var x = from o in ctx.CreateQuery<GenericEntity>(_tableName) select o;
                GenericEntity all = x.First();

                List<string> props = new List<string>();
                props.Add("PartitionKey");
                props.Add("RowKey");

                props.AddRange(all.properties.Keys);

                _columnNames = props.ToArray();
            }
        }

        public override IEnumerable<Row> Rows
        {
            get
            {
                InitColumnNames();
                var ctx = _tableClient.GetTableServiceContext();
                ctx.Format.UseAtom();
                ctx.IgnoreMissingProperties = true;
                ctx.ReadingEntity += GenericTableReader.OnReadingEntity;

                var x = from o in ctx.CreateQuery<GenericEntity>(_tableName) select o;

                var results = x.AsTableServiceQuery(ctx);

                // Convert GenericEntity to Row
                foreach (var entity in results)
                {
                    string[] values = Array.ConvertAll(_columnNames,
                        columnName =>
                        {
                            string result;
                            entity.properties.TryGetValue(columnName, out result);
                            if (result != null)
                            {
                                return result;
                            }
                            return string.Empty;
                        });
                    values[0] = entity.PartitionKey;
                    values[1] = entity.RowKey;

                    yield return new AzureRow { _parent = this, _values = values };
                }
            }
        }
    }

    // Rows must be in the same order
    internal class AzureRow : Row
    {
        public AzureStreamingTable _parent;
        public string[] _values;


        public override IEnumerable<string> ColumnNames
        {
            get { return _parent._columnNames; }
        }

        public override IList<string> Values
        {
            get { return _values; }
        }
    }
}