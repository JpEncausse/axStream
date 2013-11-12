/*****************************************************************************
 * RAOPClient.cs: RAOPClient
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
using System.Net;
using System.Collections;
using System.Net.Sockets;
using System.Globalization;
using System.Security.Cryptography;

public class RAOPClient
{
    private string host;
    private string local;
    private double volume;
    private int ajstatus;
    private int ajtype;
    private RTSPClient rc;
    private Rijndael alg;
    private TcpClient tcdata;
    private NetworkStream nsdata;
    private NumberFormatInfo nfi;

    public const double VOLUME_DEF = -30;
    public const double VOLUME_MIN = -144;
    public const double VOLUME_MAX = 0;

    public const int JACK_STATUS_DISCONNECTED = 0;
    public const int JACK_STATUS_CONNECTED = 1;

    public const int JACK_TYPE_ANALOG = 0;
    public const int JACK_TYPE_DIGITAL = 1;

    public double Volume
    {
        set
        {
            if( value >= VOLUME_MIN && value <= VOLUME_MAX )
            {
                volume = value;
                UpdateVolume();
            }
        }
    }

    public int JackStatus
    {
        get
        {
            return ajstatus;
        }
    }

    public int JackType
    {
        get
        {
            return ajtype;
        }
    }

    public RAOPClient( string Host )
    {
        host = Host;

        volume = VOLUME_DEF;
        ajstatus = JACK_STATUS_DISCONNECTED;
        ajtype = JACK_TYPE_ANALOG;

        nfi = new CultureInfo( "en-US" ).NumberFormat;

        alg = Rijndael.Create();
        alg.Mode = CipherMode.CBC;
        alg.Padding = PaddingMode.None;
        alg.KeySize = 128;

        alg.GenerateKey();
        alg.GenerateIV();

        int i = host.LastIndexOf( '.' );
        string hostnet = host.Substring( 0, i );
        IPHostEntry iphe = Dns.GetHostEntry(Dns.GetHostName());//Dns.GetHostByName( Dns.GetHostName() );
        foreach( IPAddress ipaddr in iphe.AddressList )
        {
            string s = ipaddr.ToString();
            if( s.StartsWith( hostnet ) )
            {
                local = s;
                break;
            }
        }

        if( local == null )
            local = Host;
    }

    private byte [] RSAEncrypt( byte [] PlainText )
    {
        string n =
            "59dE8qLieItsH1WgjrcFRKj6eUWqi+bGLOX1HL3U3GhC/j0Qg90u3sG/1CUtwC" +
            "5vOYvfDmFI6oSFXi5ELabWJmT2dKHzBJKa3k9ok+8t9ucRqMd6DZHJ2YCCLlDR" +
            "KSKv6kDqnw4UwPdpOMXziC/AMj3Z/lUVX1G7WSHCAWKf1zNS1eLvqr+boEjXuB" +
            "OitnZ/bDzPHrTOZz0Dew0uowxf/+sG+NCK3eQJVxqcaJ/vEHKIVd2M+5qL71yJ" +
            "Q+87X6oV3eaYvt3zWZYD6z5vYTcrtij2VZ9Zmni/UAaHqn9JdsBWLUEpVviYnh" +
            "imNVvYFZeCXg/IdTQ+x4IRdiXNv5hEew==";
        string e = "AQAB";

        RSAParameters key = new RSAParameters();
        key.Modulus = Convert.FromBase64String( n );
        key.Exponent = Convert.FromBase64String( e );
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters( key );

        return rsa.Encrypt( PlainText, true );
    }

    public void Connect()
    {
        byte [] rbs = new byte[ 4 + 8 + 16 ];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes( rbs );

        string sid = String.Format( "{0:D10}",
            BitConverter.ToUInt32( rbs, 0 ) );
        string sci = String.Format( "{0:X16}",
            BitConverter.ToUInt64( rbs, 4 ) );
        string sac = Convert.ToBase64String( rbs, 12, 16 );

        string url = String.Format( "rtsp://{0}/{1}", local, sid );
        rc = new RTSPClient( host, 5000, url );
        rc.UserAgent = "iTunes/4.6 (Macintosh; U; PPC Mac OS X 10.3)";
        rc.AddHeaders.Set( "Client-Instance", sci );
        rc.Connect();

        string key = Convert.ToBase64String( RSAEncrypt( alg.Key ) );
        string iv = Convert.ToBase64String( alg.IV );

        string sdp = String.Format(
            "v=0\r\n" +
            "o=iTunes {0} 0 IN IP4 {1}\r\n" +
            "s=iTunes\r\n" +
            "c=IN IP4 {2}\r\n" +
            "t=0 0\r\n" +
            "m=audio 0 RTP/AVP 96\r\n" +
            "a=rtpmap:96 AppleLossless\r\n" +
            "a=fmtp:96 4096 0 16 40 10 14 2 255 0 0 44100\r\n" +
            "a=rsaaeskey:{3}\r\n" +
            "a=aesiv:{4}\r\n",
            sid, local, host,
            key.Replace( "=", "" ),
            iv.Replace( "=", "" ) );

        rc.AddHeaders.Set( "Apple-Challenge", sac.Replace( "=", "" ) );
        rc.AnnounceSDP( sdp );
        rc.AddHeaders.Remove( "Apple-Challenge" );

        Hashtable ht = rc.Setup();
        string aj = (string)ht[ "Audio-Jack-Status" ];
        if( aj == null )
            throw new Exception( "Audio-Jack-Status is missing" );

        string [] ptokens = aj.Split( new char[] { ';' } );
        for( int i = 0; i < ptokens.Length; i++ )
        {
            string [] ctokens = ptokens[ i ].Split( new char[] { '=' } );
            for( int j = 0; j < ctokens.Length; j++ )
            {
                if( ctokens.Length == 1 &&
                    ctokens[ 0 ].Trim().Equals( "connected" ) )
                {
                    ajstatus = JACK_STATUS_CONNECTED;
                }
                else if( ctokens.Length == 2 &&
                         ctokens[ 0 ].Trim().Equals( "type" ) )
                {
                    if( ctokens[ 1 ].Trim().Equals( "digital" ) )
                        ajtype = JACK_TYPE_DIGITAL;
                }
            }
        }

        rc.Record();

        UpdateVolume();

        tcdata = new TcpClient();
        tcdata.Connect( host, rc.ServerPort );
        nsdata = tcdata.GetStream();
    }

    public void Disconnect()
    {
        if( tcdata != null )
            tcdata.Close();

        rc.Teardown();
        rc.Disconnect();
    }

    private void UpdateVolume()
    {
        if( rc != null )
        {
            string p = String.Format( "volume: {0}\r\n",
                                      volume.ToString( "N6", nfi ) );
            rc.SetParameter( p );
        }
    }

    private void Encrypt( byte [] Buffer, int Offset, int Count )
    {
        MemoryStream ms = new MemoryStream();
        ICryptoTransform ct = alg.CreateEncryptor();

        CryptoStream cs = new CryptoStream( ms, ct, CryptoStreamMode.Write );
        cs.Write( Buffer, Offset, (Count / 16) * 16 );
        cs.Close();

        ms.ToArray().CopyTo( Buffer, Offset );
    }

    public void SendSample(byte[] Sample)
    {
        this.SendSample(Sample, 0, Sample.Length);
    }
    public void SendSample( byte [] Sample, int Pos, int Count )
    {
        byte [] header = new byte[ 16 ]
        {
            0x24, 0x00, 0x00, 0x00,
            0xF0, 0xFF, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        byte [] data = new byte[ Count + header.Length ];
        header.CopyTo( data, 0 );

        short len = Convert.ToInt16( Count + 12 );
        byte [] ab = BitConverter.GetBytes( len );
        if( BitConverter.IsLittleEndian )
            Array.Reverse( ab, 0, ab.Length );
        ab.CopyTo( data, 2 );

        Buffer.BlockCopy( Sample, Pos, data, header.Length, Count );
        Encrypt( data, header.Length, Count );

        nsdata.Write( data, 0, data.Length );
    }
}
