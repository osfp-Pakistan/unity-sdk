﻿/**
* Copyright 2015 IBM Corp. All Rights Reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*      http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*
* @author Richard Lyle (rolyle@us.ibm.com)
*/

using UnityEngine;
using System.Collections.Generic;
using IBM.Watson.Connection;
using IBM.Watson.Utilities;
using IBM.Watson.Logging;
using System.Text;
using MiniJSON;

namespace IBM.Watson.Services.v1
{
    /// <summary>
    /// This class wraps the TextToSpeech service.
    /// </summary>
    /// <a href="http://www.ibm.com/smarterplanet/us/en/ibmwatson/developercloud/text-to-speech.html">TextToSpeech Service</a>
    public class TextToSpeech 
    {
        #region Public Types

        /// <summary>
        /// This callback is passed into the ToSpeech() method.
        /// </summary>
        /// <param name="clip">The AudioClip containing the audio to play.</param>
        public delegate void ToSpeechCallback(AudioClip clip);

        /// <summary>
        /// Audio format types that can be requested from the service.
        /// </summary>
        public enum AudioFormatType
        {
            OGG = 0,
            WAV,                    //Currently used
            FLAC
        };

        /// <summary>
        /// The available voices for synthesized speech.
        /// </summary>
        public enum VoiceType
        {
            en_US_Michael = 0,
            en_US_Lisa,
            en_US_Allison,
            en_GB_Kate,
            es_ES_Enrique,
            es_ES_Laura,
            es_US_Sofia,
            de_DE_Dieter,
            de_DE_Birgit,
            fr_FR_Renee,
            it_IT_Francesca,
            ja_JP_Emi,
        };
        #endregion

        #region Private Data
        private VoiceType m_Voice = VoiceType.en_US_Michael;
        private AudioFormatType m_AudioFormat = AudioFormatType.WAV;
        private Dictionary<VoiceType, string> m_VoiceTypes = new Dictionary<VoiceType, string>()
        {
            { VoiceType.en_US_Michael, "en-US_MichaelVoice" },
            { VoiceType.en_US_Lisa, "en-US_LisaVoice" },
            { VoiceType.en_US_Allison, "en-US_AllisonVoice" },
            { VoiceType.en_GB_Kate, "en-GB_KateVoice" },
            { VoiceType.es_ES_Enrique, "es-ES_EnriqueVoice" },
            { VoiceType.es_ES_Laura, "es-ES_LauraVoice" },
            { VoiceType.es_US_Sofia, "es-US_SofiaVoice" },
            { VoiceType.de_DE_Dieter, "de-DE_DieterVoice" },
            { VoiceType.de_DE_Birgit, "de-DE_BirgitVoice" },
            { VoiceType.fr_FR_Renee, "fr-FR_ReneeVoice" },
            { VoiceType.it_IT_Francesca, "it-IT_FrancescaVoice" },
            { VoiceType.ja_JP_Emi, "ja-JP_EmiVoice" },
        };
        private Dictionary<AudioFormatType, string> m_AudioFormats = new Dictionary<AudioFormatType, string>()
        {
            { AudioFormatType.OGG, "audio/ogg;codecs=opus" },
            { AudioFormatType.WAV, "audio/wav" },
            { AudioFormatType.FLAC, "audio/flac" },
        };
        private const string SERVICE_ID = "TextToSpeechV1";
        #endregion

        #region Public Properties
        /// <summary>
        /// This property allows the user to set the AudioFormat to use. Currently, only WAV is supported.
        /// </summary>
        public AudioFormatType AudioFormat { get { return m_AudioFormat; } set { m_AudioFormat = value; } }
        /// <summary>
        /// This property allows the user to specify the voice to use.
        /// </summary>
        public VoiceType Voice { get { return m_Voice; } set { m_Voice = value; } }
        #endregion

        #region ToSpeech Functions

        /// <summary>
        /// Private Request object that holds data specific to the ToSpeech request.
        /// </summary>
        private class ToSpeechRequest : RESTConnector.Request
        {
            public string Text { get; set; }
            public ToSpeechCallback Callback { get; set; }
        }

        /// <summary>
        /// Converts the given text into an AudioClip that can be played.
        /// </summary>
        /// <param name="text">The text to synthesis into speech.</param>
        /// <param name="callback">The callback to invoke with the AudioClip.</param>
        /// <param name="usePost">If true, then we use post instead of get, this allows for text that exceeds the 5k limit.</param>
        /// <returns>Returns true if the request is sent.</returns>
        public bool ToSpeech(string text, ToSpeechCallback callback, bool usePost = false )
        {
            if ( !m_AudioFormats.ContainsKey(m_AudioFormat) )
            {
                Log.Error( "TextToSpeech", "Unsupported audio format: {0}", m_AudioFormat.ToString() );
                return false;
            }
            if ( !m_VoiceTypes.ContainsKey(m_Voice) )
            {
                Log.Error( "TextToSpeech", "Unsupported voice: {0}", m_Voice.ToString() );
                return false;
            }

            RESTConnector connector = RESTConnector.GetConnector( SERVICE_ID, "/v1/synthesize" );
            if (connector == null)
            {
                Log.Error( "TextToSpeech", "Failed to get connector." );
                return false;
            }

            ToSpeechRequest req = new ToSpeechRequest();
            req.Text = text;
            req.Callback = callback;
            req.Parameters["accept"] = m_AudioFormats[m_AudioFormat];
            req.Parameters["voice"] = m_VoiceTypes[m_Voice];
            req.OnResponse = ToSpeechResponse;

            if (connector.UsingGateway || usePost )
            {
                Dictionary<string,string> upload = new Dictionary<string, string>();
                upload["text"] = text;

                req.Send = Encoding.UTF8.GetBytes( Json.Serialize( upload ) ); 
                req.Headers["Content-Type"] = "application/json";              
            }
            else
            {
                req.Parameters["text"] = text;
            }

            return connector.Send(req);
        }

        private void ToSpeechResponse(RESTConnector.Request req, RESTConnector.Response resp)
        {
            ToSpeechRequest speechReq = req as ToSpeechRequest;
            if (speechReq == null)
                throw new WatsonException("Wrong type of request object.");

            Log.Debug( "TextToSpeech", "Request completed in {0} seconds.", resp.ElapsedTime );

            AudioClip clip = null;
            if (resp.Success)
            {
                switch (m_AudioFormat)
                {
                    case AudioFormatType.WAV:
                        clip = WaveFile.ParseWAV(speechReq.Text, resp.Data);
                        break;
                    default:
                        Log.Error("TextToSpeech", "Unsupported audio format: {0}", m_AudioFormat.ToString());
                        break;
                }
            }
            else
            {
                Log.Error("TextToSpeech", "Request Failed: {0}", resp.Error);
            }

            if (speechReq.Callback != null)
                speechReq.Callback(clip);
        }
        #endregion
    }

}