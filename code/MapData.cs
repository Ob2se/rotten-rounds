using Sandbox;
using System;

public class MapData
{
	public string Name { get; set; }

	public string Description { get; set; }

	public string Author { get; set; }

	public int UpVotes { get; set; }

	public int DownVotes { get; set; }

	public string Thumbnail { get; set; }

	public List<string> Tags { get; set; }

	public List<string> Genres { get; set; }

	public string Indent { get; set; }

	public string FullIndent { get; set; }


	public DateTimeOffset LastUpdate { get; set; }
}
