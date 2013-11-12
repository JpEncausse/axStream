/*****************************************************************************
 * RTSPClient.cs: RTSPClient
 *****************************************************************************
 * Copyright (C) 2005 Jon Lech Johansen <jon@nanocrew.net>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111, USA.
 *****************************************************************************/

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;

public class RTSPClient
{
    private string url;
    private string host;
    private int port;
    private string useragent;
    private int cseq;
    private string session;
    private int serverport;
    private TcpClient tcctrl;
    private StreamReader srctrl;
    private NetworkStream nsctrl;
    private WebHeaderCollection addheaders;

    public RTSPClient( string Host, int Port, string URL )
    {
        host = Host;
        port = Port;
        url = URL;
        cseq = 0;

        UserAgent = "RTSPClient";

        tcctrl = new TcpClient();
        addheaders = new WebHeaderCollection();
    }

    public string UserAgent
    {
        set
        {
            useragent = value;
        }
    }

    public WebHeaderCollection AddHeaders
    {
        get
        {
            return addheaders;
        }
    }

    public int ServerPort
    {
        get
        {
            return serverport;
        }
    }

    public void Connect()
    {
        tcctrl.Connect( host, port );
        nsctrl = tcctrl.GetStream();
        srctrl = new StreamReader( nsctrl );
    }

    public void Disconnect()
    {
        srctrl = null;
        nsctrl = null;
        tcctrl.Close();
    }

    public Hashtable ExecRequest( string Cmd, string ContentType,
                                  string Content, WebHeaderCollection hds,
                                  bool GetResponse )
    {
        byte [] buf;
        string line;

        string req = String.Format(
            "{0} {1} RTSP/1.0\r\n" + "CSeq: {2}\r\n",
            Cmd, url, ++cseq );

        if( session != null )
            req += String.Format( "Session: {0}\r\n", session );

        if( hds != null )
        {
            for( int i = 0; i < hds.Count; i++ )
            {
                req += String.Format( "{0}: {1}\r\n",
                    hds.GetKey( i ), hds.Get( i ) );
            }
        }

        if( ContentType != null && Content != null )
        {
            req += String.Format(
                "Content-Type: {0}\r\n" + "Content-Length: {1}\r\n",
                ContentType, Content.Length );
        }

        req += String.Format( "User-Agent: {0}\r\n", useragent );

        for( int i = 0; i < addheaders.Count; i++ )
        {
            req += String.Format( "{0}: {1}\r\n",
                addheaders.GetKey( i ), addheaders.Get( i ) );
        }

        req += "\r\n";

        if( ContentType != null && Content != null )
            req += Content;

        buf = Encoding.ASCII.GetBytes( req );
        nsctrl.Write( buf, 0, buf.Length );

        if( !GetResponse )
            return null;

        line = srctrl.ReadLine();
        if( line == null || line == "" )
            throw new Exception( "Request failed, read error" );

        string [] tokens = line.Split( new char[] { ' ' } );
        if( tokens.Length != 3 || tokens[ 1 ] != "200" )
            throw new Exception( "Request failed, error " + tokens[ 1 ] );

        string name = null;
        Hashtable ht = new Hashtable();
        while( ( line = srctrl.ReadLine() ) != null && line != "" )
        {
            if( name != null && Char.IsWhiteSpace( line[ 0 ] ) )
            {
                ht[ name ] += line;
                continue;
            }

            int i = line.IndexOf( ":" );
            if( i == -1 )
                throw new Exception( "Request failed, bad header" );

            name = line.Substring( 0, i );
            ht[ name ] = line.Substring( i + 1 ).Trim();
        }

        return ht;
    }

    public void AnnounceSDP( string SDP )
    {
        ExecRequest( "ANNOUNCE", "application/sdp", SDP, null, true );
    }

    public Hashtable Setup()
    {
        Hashtable ht;
        WebHeaderCollection hds = new WebHeaderCollection();

        hds.Set( "Transport",
                 "RTP/AVP/TCP;unicast;interleaved=0-1;mode=record" );
        ht = ExecRequest( "SETUP", null, null, hds, true );

        session = (string)ht[ "Session" ];
        if( session == null )
            throw new Exception( "SETUP: no session in response" );

        string transport = (string)ht[ "Transport" ];
        if( transport == null )
            throw new Exception( "SETUP: no transport in response" );

        string [] ptokens = transport.Split( new char[] { ';' } );
        for( int i = 0; i < ptokens.Length; i++ )
        {
            string [] ctokens = ptokens[ i ].Split( new char [] { '=' } );
            if( ctokens.Length == 2 &&
                ctokens[ 0 ].Equals( "server_port" ) )
            {
                serverport = Convert.ToInt32( ctokens[ 1 ] );
            }
        }

        if( serverport == 0 )
            throw new Exception( "SETUP: no server_port in response" );

        return ht;
    }

    public void Record()
    {
        WebHeaderCollection hds = new WebHeaderCollection();

        if( session == null )
            throw new Exception( "RECORD: no session in progress" );

        hds.Set( "Range", "npt=0-" );
        hds.Set( "RTP-Info", "seq=0;rtptime=0" );

        ExecRequest( "RECORD", null, null, hds, true );
    }

    public void SetParameter( string Parameter )
    {
        ExecRequest( "SET_PARAMETER", "text/parameters",
                     Parameter, null, true );
    }

    public void Flush()
    {
        WebHeaderCollection hds = new WebHeaderCollection();
        hds.Set( "RTP-Info", "seq=0;rtptime=0" );
        ExecRequest( "FLUSH", null, null, hds, true );
    }

    public void Teardown()
    {
        ExecRequest( "TEARDOWN", null, null, null, false );
    }
}
