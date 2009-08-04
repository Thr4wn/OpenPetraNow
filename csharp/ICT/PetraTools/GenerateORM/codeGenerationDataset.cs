/*************************************************************************
 *
 * DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * @Authors:
 *       timop
 *
 * Copyright 2004-2009 by OM International
 *
 * This file is part of OpenPetra.org.
 *
 * OpenPetra.org is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenPetra.org is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
 *
 ************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml;
using System.IO;
using System.Data;
using Ict.Tools.CodeGeneration;
using Ict.Tools.DBXML;
using Ict.Common.IO;
using Ict.Common;

namespace Ict.Tools.CodeGeneration.DataStore
{
    public class codeGenerationDataset
    {
        private static string StringCollectionToValuesFormattedForArray(StringCollection ANames)
        {
            bool first = true;
            string Result = String.Empty;

            foreach (string fieldname in ANames)
            {
                if (!first)
                {
                    Result += ", ";
                }

                first = false;
                Result += '"' + fieldname + '"';
            }

            return Result;
        }

        /// <summary>
        /// special version to get the original sql name of the names
        /// </summary>
        /// <param name="ATables"></param>
        /// <param name="ATableName"></param>
        /// <param name="ANames"></param>
        /// <returns></returns>
        private static string StringCollectionToValuesFormattedForArray(
            SortedList <string, TDataSetTable>ATables,
            string ATableName,
            StringCollection ANames)
        {
            TDataSetTable currentTable = null;

            foreach (TDataSetTable table in ATables.Values)
            {
                if ((table.tablealias == ATableName)
                    || (table.tablename == ATableName)
                    || (table.tableorig == ATableName)
                    || (TTable.NiceTableName(table.strName) == ATableName))
                {
                    currentTable = table;
                }
            }

            if (currentTable == null)
            {
                throw new Exception("StringCollectionToValuesFormattedForArray: cannot find table " + ATableName);
            }

            bool first = true;
            string Result = String.Empty;

            foreach (string fieldname in ANames)
            {
                if (!first)
                {
                    Result += ", ";
                }

                first = false;

                TTableField field = currentTable.GetField(fieldname);

                if (field == null)
                {
                    // might be a custom field
                    Result += '"' + fieldname + '"';
                }
                else
                {
                    // get the original sql name
                    Result += '"' + field.strName + '"';
                }
            }

            return Result;
        }

        private static void AddTableToDataset(string tabletype, string variablename, ProcessTemplate snippetDataset)
        {
            string typedTableDeklaration = "private " +
                                           tabletype +
                                           "Table Table" +
                                           variablename +
                                           ";" + Environment.NewLine;

            snippetDataset.AddToCodelet("TYPEDDATATABLES", typedTableDeklaration);

            ProcessTemplate tempSnippet;

            tempSnippet = snippetDataset.GetSnippet("TYPEDTABLEPROPERTY");
            tempSnippet.SetCodelet("TABLETYPENAME", tabletype);
            tempSnippet.SetCodelet("TABLEVARIABLENAME", variablename);
            snippetDataset.InsertSnippet("TYPEDTABLEPROPERTY", tempSnippet);

            snippetDataset.AddToCodelet("INITTABLESFRESH", "this.Tables.Add(new " +
                tabletype + "Table(\"" + variablename +
                "\"));" + Environment.NewLine);

            tempSnippet = snippetDataset.GetSnippet("INITTABLESNOTALL");
            tempSnippet.SetCodelet("TABLETYPENAME", tabletype);
            tempSnippet.SetCodelet("TABLEVARIABLENAME", variablename);
            snippetDataset.InsertSnippet("INITTABLESNOTALL", tempSnippet);

            tempSnippet = snippetDataset.GetSnippet("MAPTABLES");
            tempSnippet.SetCodelet("TABLEVARIABLENAME", variablename);
            snippetDataset.InsertSnippet("MAPTABLES", tempSnippet);

            tempSnippet = snippetDataset.GetSnippet("INITVARSTABLE");
            tempSnippet.SetCodelet("TABLETYPENAME", tabletype);
            tempSnippet.SetCodelet("TABLEVARIABLENAME", variablename);
            snippetDataset.InsertSnippet("INITVARSTABLE", tempSnippet);
        }

        private static short DataSetTableIdCounter = -1;
        
        public static void CreateTypedDataSets(String AInputXmlfile,
            String AOutputPath,
            String ANameSpace,
            TDataDefinitionStore store,
            string[] groups,
            string AFilename)
        {
            Console.WriteLine("processing dataset " + ANameSpace);

            TAppSettingsManager opts = new TAppSettingsManager(false);
            string templateDir = opts.GetValue("TemplateDir", true);
            ProcessTemplate Template = new ProcessTemplate(templateDir + Path.DirectorySeparatorChar +
                "ORM" + Path.DirectorySeparatorChar +
                "DataSet.cs");
            Template.AddSnippetsFromOtherFile(templateDir + Path.DirectorySeparatorChar +
                "ORM" + Path.DirectorySeparatorChar +
                "DataTable.cs");
            
            DataSetTableIdCounter = Convert.ToInt16(opts.GetValue("StartTableId"));

            // load default header with license and copyright
            StreamReader sr = new StreamReader(templateDir + Path.DirectorySeparatorChar + "EmptyFileComment.txt");
            string fileheader = sr.ReadToEnd();
            sr.Close();
            fileheader = fileheader.Replace(">>>> Put your full name or just a shortname here <<<<", "auto generated");
            Template.SetCodelet("GPLFILEHEADER", fileheader);

            Template.SetCodelet("NAMESPACE", ANameSpace);

            // if no dataset is defined yet in the xml file, the following variables can be empty
            Template.AddToCodelet("USINGNAMESPACES", "");
            Template.AddToCodelet("CONTENTDATASETSANDTABLESANDROWS", "");

            TXMLParser parserDataSet = new TXMLParser(AInputXmlfile, false);
            XmlDocument myDoc = parserDataSet.GetDocument();
            XmlNode startNode = myDoc.DocumentElement;

            if (startNode.Name.ToLower() == "petradatasets")
            {
                XmlNode cur = TXMLParser.NextNotBlank(startNode.FirstChild);

                while ((cur != null) && (cur.Name.ToLower() == "importunit"))
                {
                    Template.AddToCodelet("USINGNAMESPACES", "using " + TXMLParser.GetAttribute(cur, "name") + ";" + Environment.NewLine);
                    cur = TXMLParser.GetNextEntity(cur);
                }

                while ((cur != null) && (cur.Name.ToLower() == "dataset"))
                {
                    ProcessTemplate snippetDataset = Template.GetSnippet("TYPEDDATASET");
                    string datasetname = TXMLParser.GetAttribute(cur, "name");
                    snippetDataset.SetCodelet("DATASETNAME", datasetname);

                    // INITCONSTRAINTS and INITRELATIONS can be empty
                    snippetDataset.AddToCodelet("INITCONSTRAINTS", "");
                    snippetDataset.AddToCodelet("INITRELATIONS", "");

                    SortedList <string, TDataSetTable>tables = new SortedList <string, TDataSetTable>();

                    XmlNode curChild = cur.FirstChild;

                    while (curChild != null)
                    {
                        if (curChild.Name.ToLower() == "table")
                        {
                            bool OverloadTable = false;
                            string tabletype = TTable.NiceTableName(TXMLParser.GetAttribute(curChild, "sqltable"));
                            string variablename = (TXMLParser.HasAttribute(curChild, "name") ?
                                                   TXMLParser.GetAttribute(curChild, "name") :
                                                   tabletype);

                            TDataSetTable table = new TDataSetTable(
                                TXMLParser.GetAttribute(curChild, "sqltable"),
                                tabletype,
                                variablename,
                                store.GetTable(tabletype));
                            XmlNode tableNodes = curChild.FirstChild;

                            while (tableNodes != null)
                            {
                                if (tableNodes.Name.ToLower() == "customfield")
                                {
                                    // eg. BestAddress in PartnerEditTDS.PPartnerLocation
                                    TTableField customField = new TTableField();
                                    customField.strName = TXMLParser.GetAttribute(tableNodes, "name");
                                    customField.strTypeDotNet = TXMLParser.GetAttribute(tableNodes, "type");
                                    customField.strDescription = TXMLParser.GetAttribute(tableNodes, "comment");
                                    table.grpTableField.List.Add(customField);

                                    OverloadTable = true;
                                }

                                if (tableNodes.Name.ToLower() == "field")
                                {
                                    // eg. UnitName in PartnerEditTDS.PPerson
                                    TTableField field = new TTableField(store.GetTable(TXMLParser.GetAttribute(tableNodes, "sqltable")).
                                        GetField(TXMLParser.GetAttribute(tableNodes, "sqlfield")));

                                    if (TXMLParser.HasAttribute(tableNodes, "name"))
                                    {
                                        field.strNameDotNet = TXMLParser.GetAttribute(tableNodes, "name");
                                    }

                                    table.grpTableField.List.Add(field);

                                    OverloadTable = true;
                                }

                                tableNodes = tableNodes.NextSibling;
                            }

                            tables.Add(TXMLParser.GetAttribute(curChild, "sqltable"), table);

                            if (OverloadTable)
                            {
                                tabletype = datasetname + TTable.NiceTableName(table.strName);

                                table.strName = tabletype;
                                // set tableid
                                table.order = DataSetTableIdCounter++;

                                // TODO: can we derive from the base table, and just overload a few functions?
                                codeGenerationTable.InsertTableDefinition(snippetDataset, table, "TABLELOOP");
                                codeGenerationTable.InsertRowDefinition(snippetDataset, table, "TABLELOOP");
                            }

                            AddTableToDataset(tabletype, variablename, snippetDataset);
                        }

                        if (curChild.Name.ToLower() == "customrelation")
                        {
                            ProcessTemplate tempSnippet = Template.GetSnippet("INITRELATIONS");
                            tempSnippet.SetCodelet("RELATIONNAME", TXMLParser.GetAttribute(curChild, "name"));
                            tempSnippet.SetCodelet("TABLEVARIABLENAMEPARENT", TXMLParser.GetAttribute(curChild, "parentTable"));
                            tempSnippet.SetCodelet("TABLEVARIABLENAMECHILD", TXMLParser.GetAttribute(curChild, "childTable"));
                            tempSnippet.SetCodelet("COLUMNNAMESPARENT",
                                StringCollectionToValuesFormattedForArray(tables, TXMLParser.GetAttribute(curChild, "parentTable"),
                                    StringHelper.StrSplit(TXMLParser.GetAttribute(curChild, "parentFields"), ",")));
                            tempSnippet.SetCodelet("COLUMNNAMESCHILD",
                                StringCollectionToValuesFormattedForArray(tables, TXMLParser.GetAttribute(curChild, "childTable"),
                                    StringHelper.StrSplit(TXMLParser.GetAttribute(curChild, "childFields"), ",")));
                            tempSnippet.SetCodelet("CREATECONSTRAINTS", TXMLParser.GetBoolAttribute(curChild, "createConstraints") ? "true" : "false");
                            snippetDataset.InsertSnippet("INITRELATIONS", tempSnippet);
                        }

                        if (curChild.Name.ToLower() == "customtable")
                        {
                            string variablename = TXMLParser.GetAttribute(curChild, "name");
                            string tabletype = datasetname + TXMLParser.GetAttribute(curChild, "name");

                            XmlNode customTableNodes = curChild.FirstChild;
                            TDataSetTable customTable = new TDataSetTable(
                                tabletype,
                                tabletype,
                                variablename,
                                null);

                            // set TableId
                            customTable.order = DataSetTableIdCounter++;
                            customTable.strDescription = TXMLParser.GetAttribute(curChild, "comment");
                            customTable.strName = tabletype;

                            while (customTableNodes != null)
                            {
                                if (customTableNodes.Name.ToLower() == "customfield")
                                {
                                    TTableField customField = new TTableField();
                                    customField.strName = TXMLParser.GetAttribute(customTableNodes, "name");
                                    customField.strTypeDotNet = TXMLParser.GetAttribute(customTableNodes, "type");
                                    customField.strDescription = TXMLParser.GetAttribute(customTableNodes, "comment");
                                    customTable.grpTableField.List.Add(customField);
                                }

                                if (customTableNodes.Name.ToLower() == "field")
                                {
                                    // eg. SelectedSiteKey in PartnerEditTDS.MiscellaneousData
                                    TTableField field = new TTableField(store.GetTable(TXMLParser.GetAttribute(customTableNodes, "sqltable")).
                                        GetField(TXMLParser.GetAttribute(customTableNodes, "sqlfield")));

                                    if (TXMLParser.HasAttribute(customTableNodes, "name"))
                                    {
                                        field.strNameDotNet = TXMLParser.GetAttribute(customTableNodes, "name");
                                    }

                                    customTable.grpTableField.List.Add(field);
                                }

                                if (customTableNodes.Name.ToLower() == "primarykey")
                                {
                                    TConstraint primKeyConstraint = new TConstraint();
                                    primKeyConstraint.strName = "PK";
                                    primKeyConstraint.strThisFields = StringHelper.StrSplit(TXMLParser.GetAttribute(customTableNodes,
                                            "thisFields"), ",");
                                    customTable.grpConstraint.List.Add(primKeyConstraint);
                                }

                                customTableNodes = customTableNodes.NextSibling;
                            }

                            tables.Add(tabletype, customTable);

                            AddTableToDataset(tabletype, variablename, snippetDataset);

                            codeGenerationTable.InsertTableDefinition(snippetDataset, customTable, "TABLELOOP");
                            codeGenerationTable.InsertRowDefinition(snippetDataset, customTable, "TABLELOOP");
                        }

                        curChild = curChild.NextSibling;
                    }

                    foreach (TDataSetTable table in tables.Values)
                    {
                        // todo? also other constraints, not only from original table?
                        foreach (TConstraint constraint in table.grpConstraint.List)
                        {
                            if ((constraint.strType == "foreignkey") && tables.ContainsKey(constraint.strOtherTable))
                            {
                                TDataSetTable otherTable = (TDataSetTable)tables[constraint.strOtherTable];

                                ProcessTemplate tempSnippet = Template.GetSnippet("INITCONSTRAINTS");

                                tempSnippet.SetCodelet("TABLEVARIABLENAME1", table.tablealias);
                                tempSnippet.SetCodelet("TABLEVARIABLENAME2", otherTable.tablealias);
                                tempSnippet.SetCodelet("CONSTRAINTNAME", TTable.NiceKeyName(constraint));

                                tempSnippet.SetCodelet("COLUMNNAMES1", StringCollectionToValuesFormattedForArray(constraint.strThisFields));
                                tempSnippet.SetCodelet("COLUMNNAMES2", StringCollectionToValuesFormattedForArray(constraint.strOtherFields));
                                snippetDataset.InsertSnippet("INITCONSTRAINTS", tempSnippet);
                            }
                        }
                    }

                    Template.InsertSnippet("CONTENTDATASETSANDTABLESANDROWS", snippetDataset);

                    cur = TXMLParser.GetNextEntity(cur);
                }
            }

            Template.FinishWriting(AOutputPath + Path.DirectorySeparatorChar + AFilename + ".cs", ".cs", true);
        }
    }
}