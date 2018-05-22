using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.RightsManagement;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Database;

namespace RDB
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            bwLoad.DoWork += bwLoad_DoWork;
            bwLoad.ProgressChanged += bw_ProgressChanged;
            bwLoad.RunWorkerCompleted += bw_RunWorkerCompleted;

            bwInser.DoWork += bwIsert_DoWork;
            bwInser.ProgressChanged += bw_ProgressChanged;
            bwInser.RunWorkerCompleted += bw_RunWorkerCompleted;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressBar.IsIndeterminate = true;
            if (!bwLoad.IsBusy)
            {
                bwLoad.RunWorkerAsync();
            }

        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressBar.IsIndeterminate = true;
            if (!bwInser.IsBusy)
            {
                bwInser.RunWorkerAsync();
            }
        }

        public BackgroundWorker bwLoad = new BackgroundWorker();
        public BackgroundWorker bwInser = new BackgroundWorker();
        public void bwLoad_DoWork(object sender, DoWorkEventArgs e)
        {

            //Dispatcher.Invoke(new Action(() =>
            //{
            //    DataGrid.ItemsSource = new ObservableCollection<trip>(LSD);
            //}));
        }

        public void bwIsert_DoWork(object sender, DoWorkEventArgs e)
        {
            var LSD = new ReadDada().CSV();
            var results = new DB().InsertMySQL(LSD);
            var bad = results.Where(x => x.Equals(false)).ToList();
            Debug.WriteLine($"Wrong recors count: {bad.Count}");
        }

        public void bw_RunWorkerCompleted(object sender,
            RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { ProgressBar.IsIndeterminate = false; }));
        }

        public void bw_ProgressChanged(object sender,
            ProgressChangedEventArgs e)
        {

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                loadPasses();
            });
        }

        private void loadPasses()
        {
            using (drbEntities db = new drbEntities())
            {
                string q = $"SELECT * FROM `trip`";
                Dispatcher.Invoke(new Action(() =>
                {
                    branyCB.ItemsSource = new ObservableCollection<trip>(db.Database.SqlQuery<trip>(q).ToList());
                }));
            }
        }

        public class ReadDada
        {
            public List<trip> CSV(string path = @"D:\SKOLA\DRB\RDB1526930152.csv")
            {
                List<trip> LSD = new List<trip>();
                using (var reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        trip trip = new trip()
                        {
                            car1 = new car(),
                            driver1 = new driver(),
                            pass1 = new pass(),
                            gate1 = new gate()
                        };
                        var line = reader.ReadLine();
                        var values = line.Split(',');

                        trip.car1.spz = values[0];
                        trip.car1.color = values[1];
                        trip.car1.company = values[2];
                        trip.car1.model = values[3];
                        int.TryParse(values[7], out var distance);
                        trip.car1.distance = distance;
                        int.TryParse(values[8], out var fuel);
                        trip.car1.fuel = fuel;
                        double.TryParse(values[9].Replace(".", ","), out var voltage);
                        trip.car1.voltage = voltage;

                        trip.driver1.licence = values[4];
                        trip.driver1.name = values[5];
                        int.TryParse(values[6], out var date1);
                        trip.date1 = date1;
                        int.TryParse(values[10], out var date2);
                        trip.date2 = date2;

                        trip.gate1.gate_identificator = values[11];
                        double.TryParse(values[12].Replace(".", ","), out var longtitude);
                        trip.pass1.longtitude = longtitude;
                        double.TryParse(values[13].Replace(".", ","), out var latitude);
                        trip.pass1.latitude = latitude;
                        trip.gate1.type = values[14];
                        double.TryParse(values[15].Replace(".", ","), out var price);
                        trip.pass1.price = price;
                        LSD.Add(trip);
                    }
                }
                return LSD;
            }
        }


        public class DB
        {
            #region Insert
            public List<bool> InsertMySQL(List<trip> LSD)
            {
                Stopwatch watch = Stopwatch.StartNew();
                List<bool> results = new List<bool>();
                List<string> queryList = new List<string>();

                var drivers = LSD.

                Parallel.ForEach(LSD, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    trip =>
                    {
                        results.Add(insertTrip(trip, ref queryList));
                    });

                Debug.WriteLine($"Parallel: {watch.Elapsed.Seconds}");
                watch.Restart();

                Execute(queryList);

                Debug.WriteLine($"Execute: {watch.Elapsed.Seconds}");
                return results;
            }

            public void Execute<T>(List<T> queryList, int nSize = 1000)
            {
                var list = new List<List<T>>();

                for (int i = 0; i < queryList.Count; i += nSize)
                {
                    list.Add(queryList.GetRange(i, Math.Min(nSize, queryList.Count - i)));
                }

                using (drbEntities db = new drbEntities())
                    foreach (var queries in list)
                    {
                        db.Database.ExecuteSqlCommandAsync(string.Join("", queries));
                    }
            }

            private bool insertTrip(trip trip, ref List<string> queriList)
            {
                using (drbEntities db = new drbEntities())
                {
                    bool carOK = false;
                    bool driverOK = false;
                    bool gateOK = false;
                    string query = "";
                    string q = "";

                    lock (db.car)
                        if (db.car.Any(x => x.spz == trip.car1.spz))
                        {
                            if (db.car.Any(x =>
                                x.spz == trip.car1.spz && x.model == trip.car1.model && x.company == trip.car1.company))
                            {
                                trip.car1 = db.car.First(x =>
                                    x.spz == trip.car1.spz && x.model == trip.car1.model &&
                                    x.company == trip.car1.company);
                                carOK = true;
                            }
                            else
                                return false;
                        }
                        else
                        {
                            trip.car1 = db.car.Add(trip.car1);
                            db.SaveChanges();
                            carOK = true;
                        }

                    lock (db.driver)
                        if (db.driver.Any(x => x.licence == trip.driver1.licence))
                        {
                            if (db.driver.Any(x => x.licence == trip.driver1.licence && x.name == trip.driver1.name))
                            {
                                trip.driver1 = db.driver.First(x =>
                                    x.licence == trip.driver1.licence && x.name == trip.driver1.name);
                                driverOK = true;
                            }
                            else
                                return false;
                        }
                        else
                        {
                            trip.driver1 = db.driver.Add(trip.driver1);
                            db.SaveChanges();
                            driverOK = true;
                        }

                    lock (db.gate)
                        if (db.gate.Any(x => x.gate_identificator == trip.gate1.gate_identificator))
                        {
                            if (db.gate.Any(x =>
                                x.gate_identificator == trip.gate1.gate_identificator && x.type == trip.gate1.type))
                            {
                                trip.gate1 = db.gate.First(x =>
                                    x.gate_identificator == trip.gate1.gate_identificator && x.type == trip.gate1.type);
                                gateOK = true;
                            }
                            else
                                return false;
                        }
                        else
                        {
                            trip.gate1 = db.gate.Add(trip.gate1);
                            db.SaveChanges();
                            gateOK = true;
                        }

                    lock (db.pass)
                    {
                        trip.pass1 = db.pass.Add(trip.pass1);
                        db.SaveChanges();
                    }

                    lock (queriList)
                        queriList.Add($"INSERT INTO `trip` ( car, driver, pass, gate, date1, date2) VALUES ({trip.car1.car_id}, {trip.driver1.driver_id}, {trip.pass1.pass_id}, {trip.gate1.gate_id}, {trip.date1}, {trip.date2});");

                    return true;
                }
            }
            #endregion
        }

    }
}
