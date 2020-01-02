using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;

namespace baiduGetDomains
{
    class Program
    {
        static Queue<string> URLs = new Queue<string>();
        //список скачанных страниц
        static List<string> HTMLs = new List<string>();
        //локер для очереди адресов
        static object URLlocker = new object();
        //локер для списка скачанных страниц
        static object HTMLlocker = new object();
        //очередь ошибок
        static Queue<Exception> exceptions = new Queue<Exception>();
        static List<string> domains = new List<string>();
        static void Main(string[] args)
        {
            // чтение запросов
            string line;
            StreamReader file = new StreamReader(@"queries.txt");
            while ((line = file.ReadLine()) != null)
            {
                int incPage = 20;                
                for(int i = 0; i<15; i++)
                {
                    URLs.Enqueue("http://v.baidu.com/v?word=" + line + "&ct=905969664&pn=" + incPage * i);
                    Console.WriteLine("http://v.baidu.com/v?word=" + line + "&ct=905969664&pn=" + incPage * i);
                }
            }

            //создаем массив хендлеров, для контроля завершения потоков
            ManualResetEvent[] handles = new ManualResetEvent[3];
            //создаем и запускаем 3 потока
            for (int i = 0; i < 3; i++)
            {
                handles[i] = new ManualResetEvent(false);
                (new Thread(new ParameterizedThreadStart(Download))).Start(handles[i]);
            }
            //ожидаем, пока все потоки отработают
            WaitHandle.WaitAll(handles);
            //проверяем ошибки, если были - выводим
            foreach (Exception ex in exceptions)
                Console.WriteLine(ex.Message);
            //сохраняем закачанные страницы в файлы
            try
            {
                //for (int i = 0; i < HTMLs.Count; i++)
                    //File.WriteAllText("c:\\" + i + ".html", HTMLs[i]);
                //Console.WriteLine(HTMLs.Count + " files saved");
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            //
            Console.WriteLine("Download completed" + domains.Count());
            
            using (System.IO.StreamWriter file1 = new System.IO.StreamWriter(@"domains.txt"))
            {
                domains = domains.Distinct().ToList();

                foreach (string line1 in domains)
                {
                    // If the line doesn't contain the word 'Second', write the line to the file.
                    //if (!line1.Contains("Second"))
                    //{
                        file1.WriteLine(line1);
                    //}
                }
            }
            Console.ReadLine();
            
        }
        private static string GET(string Url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url);
            req.AllowAutoRedirect = true;
            req.UserAgent = "Mozila/14.0 (compatible; MSIE 6.0;Windows NT 5.1; SV1; MyIE2;";
            req.KeepAlive = false;
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            
            System.IO.Stream stream = resp.GetResponseStream();
            System.IO.StreamReader sr = new System.IO.StreamReader(stream);
            string Out = sr.ReadToEnd();
            sr.Close(); Console.WriteLine(Out);
            return Out;
        }
        public static void Download(object handle)
        {
            //будем крутить цикл, пока не закончатся ULR в очереди
            while (true)
            try
            {
                string URL;
                //блокируем очередь URL и достаем оттуда один адрес
                lock (URLlocker)
                {
                    if (URLs.Count == 0)
                        break;//адресов больше нет, выходим из метода, завершаем поток
                    else
                        URL = URLs.Dequeue();
                }
                Console.WriteLine(URL + " - start downloading ...");
                //скачиваем страницу

                string HTML = GET(URL);
                //блокируем список скачанных страниц, и заносим туда свою страницу
                lock (HTMLlocker)
                {
                    HTMLs.Add(HTML);
                    //парсинг страницы и сохранение результатов в файл
                    Regex newReg = new Regex("srcShortUrl:\"(?<val>.*?)\",");
                    MatchCollection matches = newReg.Matches(HTML);
                    if (matches.Count > 0)
                    {
                        foreach (Match mat in matches)
                        {
                            Console.WriteLine(mat.Groups["val"].Value);
                            domains.Add(mat.Groups["val"].Value);
                        }
                    }
                }
                //
                Console.WriteLine(URL + " - downloaded (" + HTML.Length + " bytes)");
            }
            catch (ThreadAbortException)
            {
                //это исключение возникает если главный поток хочет завершить приложение
                //просто выходим из цикла, и завершаем выполнение
                break;
            }
            catch (Exception ex)
            {
                //в процессе работы возникло исключение
                //заносим ошибку в очередь ошибок, предварительно залочив ее
                lock (exceptions)
                    exceptions.Enqueue(ex);
                //берем следующий URL
                continue;
            }
            //устанавливаем флажок хендла, что бы сообщить главному потоку о том, что мы отработали
            ((ManualResetEvent)handle).Set();
        }
    }
}
