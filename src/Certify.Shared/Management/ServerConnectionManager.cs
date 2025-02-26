﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Certify.Models.Providers;
using Newtonsoft.Json;

namespace Certify.Shared.Core.Management
{
    public class ServerConnectionManager
    {
        public static string GetAppDataFolder(string subFolder = null)
        {
            var parts = new List<string>()
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Models.SharedConstants.APPDATASUBFOLDER
            };

            if (subFolder != null)
            {
                parts.Add(subFolder);
            }

            var path = Path.Combine(parts.ToArray());

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        private static string GetConfigPath()
        {
            var appDataPath = GetAppDataFolder();
            var connectionConfigFile = Path.Combine(appDataPath, "servers.json");
            return connectionConfigFile;
        }

        /// Get list of saved server connections
        /// </summary>
        /// <returns>  </returns>
        public static List<ServerConnection> GetServerConnections(ILog log, ServerConnection defaultConfig)
        {
            var defaultConnectionList = new List<ServerConnection>();
            if (defaultConfig != null)
            {
                defaultConnectionList.Add(defaultConfig);
            }

            var connectionConfigFile = GetConfigPath();

            try
            {
                if (File.Exists(connectionConfigFile))
                {
                    var config = File.ReadAllText(connectionConfigFile);
                    if (!string.IsNullOrWhiteSpace(config))
                    {
                        var savedList = JsonConvert.DeserializeObject<List<ServerConnection>>(config);
                        if (savedList?.Any() == true)
                        {
                            return savedList;
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                // failed to load
                log?.Error($"Failed to load server connection configuration [{connectionConfigFile}] :: {exp}");
            }

            // failed to get any list of server connections, return default
            return defaultConnectionList;
        }

        public static void Save(ILog log, List<ServerConnection> connections)
        {
            if (connections == null)
            {
                return;
            }

            var connectionConfigFile = GetConfigPath();

            try
            {
                File.WriteAllText(connectionConfigFile, JsonConvert.SerializeObject(connections, Formatting.Indented));
            }
            catch (Exception exp)
            {
                // failed to save
                log?.Error($"Failed to save server connection configuration [{connectionConfigFile}] :: {exp}");
            }
        }

        public static bool ConfigExists()
        {
            var connectionConfigFile = GetConfigPath();
            return File.Exists(connectionConfigFile);
        }
    }
}
