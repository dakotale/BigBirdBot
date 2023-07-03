using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Json
{
    public class Movie
    {
        private string title = null;
        private int? year = null;
        private string rating = null;
        private string release = null;
        private string runtime = null;
        private string genre = null;
        private string director = null;
        private string writer = null;
        private string actor = null;
        private string plot = null;
        private string posterUrl = null;
        private string metaScore = null;
        private string imdbScore = null;
        private string boxOffice = null;

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public int? Year
        {
            get { return year; }
            set { year = value; }
        }

        public string Rating
        {
            get { return rating; }
            set { rating = value; }
        }

        public string Release
        {
            get { return release; }
            set { release = value; }
        }

        public string RunTime
        {
            get { return runtime; }
            set { runtime = value; }
        }

        public string Genre
        {
            get { return genre; }
            set { genre = value; }
        }

        public string Director
        {
            get { return director; }
            set { director = value; }
        }

        public string Writer
        {
            get { return writer; }
            set { writer = value; }
        }

        public string Actor
        {
            get { return actor; }
            set { actor = value; }
        }

        public string Plot
        {
            get { return plot; }
            set { plot = value; }
        }

        public string PosterURL
        {
            get { return posterUrl; }
            set { posterUrl = value; }
        }

        public string MetaScore
        {
            get { return metaScore; }
            set { metaScore = value; }
        }

        public string IMDBScore
        {
            get { return imdbScore; }
            set { imdbScore = value; }
        }

        public string BoxOffice
        {
            get { return boxOffice; }
            set { boxOffice = value; }
        }

        public Movie() { }

        public Movie(string movieDetails)
        {
            JObject obj = JObject.Parse(movieDetails);
            if (obj.Count > 0)
            {
                foreach (KeyValuePair<string, JToken> item in obj)
                {
                    string key = item.Key;
                    string value = item.Value.ToString();
                    if (key.Equals("Title"))
                    {
                        title = value;
                    }
                    if (key.Equals("Year"))
                    {
                        year = int.Parse(value);
                    }
                    if (key.Equals("Rated"))
                    {
                        rating = value;
                    }
                    if (key.Equals("Released"))
                    {
                        release = value;
                    }
                    if (key.Equals("Runtime"))
                    {
                        runtime = value;
                    }
                    if (key.Equals("Genre"))
                    {
                        genre = value;
                    }
                    if (key.Equals("Director"))
                    {
                        director = value;
                    }
                    if (key.Equals("Writer"))
                    {
                        writer = value;
                    }
                    if (key.Equals("Actors"))
                    {
                        actor = value;
                    }
                    if (key.Equals("Plot"))
                    {
                        plot = value;
                    }
                    if (key.Equals("Poster"))
                    {
                        posterUrl = value;
                    }
                    if (key.Equals("Metascore"))
                    {
                        metaScore = value;
                    }
                    if (key.Equals("imdbRating"))
                    {
                        imdbScore = value;
                    }
                    if (key.Equals("BoxOffice"))
                    {
                        boxOffice = value;
                    }
                }
            }
        }

        public List<Movie> GetMovieDetails(string results)
        {
            List<Movie> movieDetails = new List<Movie>();
            JObject obj = JObject.Parse(results);
            if (obj.Count > 0)
            {
                movieDetails.Add(new Movie(results));
            }

            return movieDetails;
        }
    }
}
