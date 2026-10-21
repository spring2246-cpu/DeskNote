using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeskNote {
public static class Weather {
 public static readonly Dictionary<string,string> Cities=new Dictionary<string,string>{{"北京","39.90&longitude=116.40"},{"上海","31.23&longitude=121.47"},{"广州","23.13&longitude=113.26"},{"深圳","22.54&longitude=114.06"},{"杭州","30.27&longitude=120.15"},{"成都","30.57&longitude=104.07"},{"武汉","30.59&longitude=114.30"},{"西安","34.34&longitude=108.94"},{"南京","32.06&longitude=118.80"},{"重庆","29.56&longitude=106.55"},{"Los Angeles","34.05&longitude=-118.24"}};
 public static Task<string> Load(string city) {return Task.Run(()=>{
  if(!Cities.ContainsKey(city))return "请选择城市";
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var request=(HttpWebRequest)WebRequest.Create("https://api.open-meteo.com/v1/forecast?latitude="+Cities[city]+"&current=temperature_2m,weather_code&timezone=auto&forecast_days=1");request.Timeout=8000;request.ReadWriteTimeout=8000;request.UserAgent="DeskNote/0.2";
  using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return Parse(reader.ReadToEnd());
 });}
 public static string Parse(string content){var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(content);var current=(Dictionary<string,object>)json["current"];if(current["temperature_2m"]==null||current["weather_code"]==null)throw new InvalidDataException("天气数据尚不可用。");int code=Convert.ToInt32(current["weather_code"]);string condition=code==0?"晴":code<=3?"多云":code<=48?"雾":code<=67?"雨":code<=77?"雪":code<=82?"阵雨":code<=86?"阵雪":"雷雨";return Convert.ToDouble(current["temperature_2m"]).ToString("0")+"°C · "+condition;}
}
}
