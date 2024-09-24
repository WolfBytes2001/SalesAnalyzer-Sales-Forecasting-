using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Generic;

namespace SalesAnalyzer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var horizon1 = Convert.ToInt32(numericUpDown1.Text);

            if (horizon1 == 0)
            {
                MessageBox.Show("Invalid input for horizon!");
            }
            else
            {
                var modelOutput = SalesAnalyzer.Predict();
                Console.WriteLine(string.Join(" , ", modelOutput.Number_sold));

                modelOutput = SalesAnalyzer.Predict(horizon: horizon1);
                Console.WriteLine(string.Join(" , ", modelOutput.Number_sold));

                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("Period", typeof(int));
                dataTable.Columns.Add("Forecasted Sales", typeof(int));
                dataTable.Columns.Add("Upper Bound", typeof(int));
                dataTable.Columns.Add("Lower Bound", typeof(int));
                for (int i = 0; i < modelOutput.Number_sold.Length; i++)
                {
                    DataRow row = dataTable.NewRow();
                    row["Period"] = i + 1;
                    row["Forecasted Sales"] = modelOutput.Number_sold[i];
                    row["Upper Bound"] = modelOutput.Number_sold_UB[i];
                    row["Lower Bound"] = modelOutput.Number_sold_LB[i];
                    dataTable.Rows.Add(row);
                }

                dataGridView1.DataSource = dataTable;
            }
            visualizeData();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var horizon1 = Convert.ToInt32(numericUpDown1.Value); // Use Value property instead of Text

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files|*.csv"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string csvFilePath = openFileDialog.FileName;
                var dataTable = LoadDataFromCsv(csvFilePath);
                dataGridView2.DataSource = dataTable;

                // Use the loaded data for predictions
                var modelInputs = LoadModelInputsFromDataTable(dataTable);

                var modelOutput = SalesAnalyzer.Predict(modelInputs.First(), horizon1); // Use horizon1 for the predict horizon
                Console.WriteLine(string.Join(" , ", modelOutput.Number_sold));

                DataTable forecastDataTable = new DataTable();
                forecastDataTable.Columns.Add("Period", typeof(int));
                forecastDataTable.Columns.Add("Forecasted Sales", typeof(int));
                forecastDataTable.Columns.Add("Upper Bound", typeof(int));
                forecastDataTable.Columns.Add("Lower Bound", typeof(int));

                for (int i = 0; i < modelOutput.Number_sold.Length; i++)
                {
                    DataRow row = forecastDataTable.NewRow();
                    row["Period"] = i + 1;
                    row["Forecasted Sales"] = modelOutput.Number_sold[i];
                    row["Upper Bound"] = modelOutput.Number_sold_UB[i];
                    row["Lower Bound"] = modelOutput.Number_sold_LB[i];
                    forecastDataTable.Rows.Add(row);
                }

                dataGridView1.DataSource = forecastDataTable;
            }
            visualizeData();
        }


        private DataTable LoadDataFromCsv(string csvFilePath)
        {
            var dataTable = new DataTable();

            using (var reader = new StreamReader(csvFilePath))
            {
                bool firstRow = true;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    if (firstRow)
                    {
                        foreach (var value in values)
                        {
                            dataTable.Columns.Add(value.Trim());
                        }
                        firstRow = false;
                    }
                    else
                    {
                        var newRow = dataTable.NewRow();
                        for (int i = 0; i < values.Length; i++)
                        {
                            newRow[i] = values[i].Trim();
                        }
                        dataTable.Rows.Add(newRow);
                    }
                }
            }

            return dataTable;
        }

        private List<SalesAnalyzer.ModelInput> LoadModelInputsFromDataTable(DataTable dataTable)
        {
            var modelInputs = new List<SalesAnalyzer.ModelInput>();

            foreach (DataRow row in dataTable.Rows)
            {
                modelInputs.Add(new SalesAnalyzer.ModelInput
                {
                    Number_sold = Convert.ToSingle(row["Number_sold"])
                });
            }

            return modelInputs;
        }

        private void TrainModelFromDataTable(DataTable dataTable, string outputModelPath)
        {
            var mlContext = new MLContext();

            var data = mlContext.Data.LoadFromEnumerable(dataTable.AsEnumerable().Select(row =>
            {
                return new ModelInput
                {
                    Sales = Convert.ToSingle(row["Sales"])
                };
            }));

            var pipeline = SalesAnalyzer.BuildPipeline(mlContext);
            var model = pipeline.Fit(data);

            SalesAnalyzer.SaveModel(mlContext, model, data, outputModelPath);
        }

        public class ModelInput
        {
            public float Sales { get; set; }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            visualizeData();
        }

        private void visualizeData()
        {
            // Clear previous chart series
            chart1.Series.Clear();

            // Create a new series
            Series forecastSeries = new Series
            {
                Name = "Forecasted Sales",
                Color = System.Drawing.Color.Blue,
                ChartType = SeriesChartType.Line
            };

            Series upperBoundSeries = new Series
            {
                Name = "Upper Bound",
                Color = System.Drawing.Color.Green,
                ChartType = SeriesChartType.Line
            };

            Series lowerBoundSeries = new Series
            {
                Name = "Lower Bound",
                Color = System.Drawing.Color.Red,
                ChartType = SeriesChartType.Line
            };

            // Add the series to the chart
            chart1.Series.Add(forecastSeries);
            chart1.Series.Add(upperBoundSeries);
            chart1.Series.Add(lowerBoundSeries);

            // Populate the chart from DataGridView
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Period"].Value != null && row.Cells["Forecasted Sales"].Value != null)
                {
                    int period = Convert.ToInt32(row.Cells["Period"].Value);
                    int forecastedSales = Convert.ToInt32(row.Cells["Forecasted Sales"].Value);
                    int upperBound = Convert.ToInt32(row.Cells["Upper Bound"].Value);
                    int lowerBound = Convert.ToInt32(row.Cells["Lower Bound"].Value);

                    forecastSeries.Points.AddXY(period, forecastedSales);
                    upperBoundSeries.Points.AddXY(period, upperBound);
                    lowerBoundSeries.Points.AddXY(period, lowerBound);
                }
            }

            // Optionally, adjust the chart area settings for better visualization
            chart1.ChartAreas[0].RecalculateAxesScale();
        }

    }
}
