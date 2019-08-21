using Ionic.Zip;
using JenkinsNET.Models;
using SharpSvn;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace IncrementalRelease
{
    class Program
    {
        static void Main(string[] args)
        {
            var jobname = args[0];
            var filepath = args[1];
            var jobpath = args[2];
            var svnstr = args[3];
            var lastVersionNo = args[4];

            using (SvnClient client = GetSvnClient())
            {
                string url = svnstr;

                SvnLogArgs arg = new SvnLogArgs();
                System.Collections.ObjectModel.Collection<SvnLogEventArgs> logs;
                client.GetLog(new Uri(url), arg, out logs);

                ZipPatch(logs.ToList(), jobname, filepath, jobpath, lastVersionNo);
            }
        }

        private static void ZipPatch(List<SvnLogEventArgs> logList, string jobname, string srcpath, string jobpath, string lastVersionNo)
        {
            string host = ConfigurationManager.AppSettings["jenkinshost"];
            string username = ConfigurationManager.AppSettings["jenkinsuser"];
            string password = ConfigurationManager.AppSettings["jenkinspwd"];
            string packname = ConfigurationManager.AppSettings["packname"];

            XmlDocument doc = new XmlDocument();
            doc.Load(AppDomain.CurrentDomain.BaseDirectory + "\\PathCheck.xml");
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (XmlNode c in doc.SelectSingleNode("path"))
            {
                if (!dic.ContainsKey(c.InnerText) && c.Attributes.Count > 0)
                    dic.Add(c.InnerText, c.Attributes[0].Value);
            }

            JenkinsNET.JenkinsClient jc = new JenkinsNET.JenkinsClient()
            {
                UserName = username,
                Password = password,
                BaseUrl = host
            };

            var jb = jc.Jobs.Get<JenkinsWorkflowJob>(jobname);
            var buildResult = jc.Builds.Get<JenkinsBuildBase>(jobname, lastVersionNo == "0" ? jb.LastSuccessfulBuild.Number.ToString() : lastVersionNo);

            var logs = logList.Where(x => x.Time > GetTime(buildResult.TimeStamp.Value, true));
            Dictionary<string, string> tempdic = new Dictionary<string, string>();

            foreach (var log in logs)
            {
                foreach (var path in log.ChangedPaths)
                {
                    switch (path.Path.Substring(path.Path.LastIndexOf(".") + 1).ToLower())
                    {
                        case "cs":
                            var dickey = dic.Where(x => Regex.Match(path.Path, x.Key).Success).OrderByDescending(x => x.Key.Length);
                            if (dickey.Any() && !tempdic.ContainsKey(dickey.First().Key))
                            {
                                tempdic.Add(dickey.First().Key, dickey.First().Value);
                            }
                            break;
                        case "sql":
                            break;
                        case "csproj":
                            break;
                        case "pubxml":
                            break;
                        case "resx":
                            break;
                        case "asax":
                            break;
                        case "lic":
                            break;
                        case "user":
                            break;
                        case "css":
                        case "html":
                        case "png":
                        case "jpg":
                        case "js":
                        case "xml":
                        case "cshtml":
                        case "asmx":
                            //非dll文件添加完整路径
                            var relativepath = path.Path.Substring(path.Path.LastIndexOf(packname) + packname.Length);
                            tempdic.Add(path.Path, relativepath);
                            break;
                        default:
                            break;
                    }
                }
            }

            using (ZipFile zip = new ZipFile(Encoding.Default))
            {
                foreach (var t in tempdic)
                {
                    // add this map file into the "images" directory in the zip archive

                    if (t.Value.IndexOf('.') > 0)
                    {
                        zip.AddFile(srcpath + (t.Value), t.Value);
                    }
                }
                zip.AddDirectory(jobpath+"\\脚本", "脚本");
                zip.Save(srcpath + "\\patch" + DateTime.Now.ToString("_yyyyMMdd") + ".zip");
            }

        }

        private static SvnClient GetSvnClient()
        {
            SvnClient client = new SvnClient();
            client.Authentication.Clear();
            client.Authentication.UserNamePasswordHandlers += Authentication_UserNamePasswordHandlers;
            client.Authentication.SslServerTrustHandlers += Authentication_SslServerTrustHandlers;
            return client;
        }

        private static void Authentication_UserNamePasswordHandlers(object sender, SharpSvn.Security.SvnUserNamePasswordEventArgs e)
        {
            //登录SVN的用户名和密码
            e.UserName = ConfigurationManager.AppSettings["svnuser"];
            e.Password = ConfigurationManager.AppSettings["svnpwd"];
        }

        //SSL证书有问题的，如果要忽略的话可以在这里忽略
        private static void Authentication_SslServerTrustHandlers(object sender, SharpSvn.Security.SvnSslServerTrustEventArgs e)
        {
            e.AcceptedFailures = e.Failures;
            e.Save = true;
        }

        public static DateTime GetTime(long TimeStamp, bool AccurateToMilliseconds = false)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            if (AccurateToMilliseconds)
            {
                return startTime.AddTicks(TimeStamp * 10000);
            }
            else
            {
                return startTime.AddTicks(TimeStamp * 10000000);
            }
        }
    }

}

