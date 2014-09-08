﻿/*
 * Tracker.cs
 * 
 * Copyright (c) 2014 Snowplow Analytics Ltd. All rights reserved.
 * This program is licensed to you under the Apache License Version 2.0,
 * and you may not use this file except in compliance with the Apache License
 * Version 2.0. You may obtain a copy of the Apache License Version 2.0 at
 * http://www.apache.org/licenses/LICENSE-2.0.
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the Apache License Version 2.0 is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the Apache License Version 2.0 for the specific
 * language governing permissions and limitations there under.
 * Authors: Fred Blundun
 * Copyright: Copyright (c) 2014 Snowplow Analytics Ltd
 * License: Apache License Version 2.0
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using SelfDescribingJson = System.Collections.Generic.Dictionary<string, object>;
using Context = System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>;

namespace Snowplow.Tracker
{
    public class Tracker
    {
        private string collectorUri;
        private bool b64;
        private Dictionary<string, string> standardNvPairs;

        public Tracker(string endpoint, string trackerNamespace = null, string appId = null, bool encodeBase64 = true)
        {
            collectorUri = getCollectorUri(endpoint);
            b64 = encodeBase64;
            standardNvPairs = new Dictionary<string, string>
            {
                { "tv", Version.VERSION },
                { "tna", trackerNamespace },
                { "aid", appId },
                { "p", "pc" }
            };
        }

        private string getCollectorUri(string endpoint)
        {
            return "http://" + endpoint + "/i";
        }

        public Tracker setPlatform(string value)
        {
            standardNvPairs["p"] = value;
            return this;
        }

        public Tracker setUserId(string id)
        {
            standardNvPairs["uid"] = id;
            return this;
        }

        public Tracker setScreenResolution(int width, int height)
        {
            standardNvPairs["res"] = width.ToString() + "x" + height.ToString();
            return this;
        }

        public Tracker setViewport(int width, int height)
        {
            standardNvPairs["vp"] = width.ToString() + "x" + height.ToString();
            return this;
        }

        public Tracker setColorDepth(int depth)
        {
            standardNvPairs["cd"] = depth.ToString();
            return this;
        }

        public Tracker setTimezone(string timezone)
        {
            standardNvPairs["tz"] = timezone;
            return this;
        }

        public Tracker setLang(string lang)
        {
            standardNvPairs["lang"] = lang;
            return this;
        }

        private static Int64 getTimestamp(Int64? tstamp)
        {
            return tstamp ?? (Int64)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
        }

        private static string ToQueryString(Dictionary<string, string> payload)
        {
            var array = (from key in payload.Keys
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(payload[key])))
                .ToArray();
            return "?" + string.Join("&", array);
        }

        private void httpGet(Dictionary<string, string> payload)
        {
            string destination = collectorUri + ToQueryString(payload);
            Console.WriteLine("destination: " + destination); // TODO remove debug code
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(destination);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        }

        private void track(Payload pb)
        {
            httpGet(pb.NvPairs);
        }

        private void completePayload(Payload pb, Context context, Int64? tstamp)
        {
            pb.add("dtm", getTimestamp(tstamp));
            pb.add("eid", Guid.NewGuid().ToString());
            if (context != null && context.Any())
            {
                var contextEnvelope = new Dictionary<string, object>
                {
                    { "schema", "iglu:com.snowplowanalytics.snowplow/contexts/1-0-0" },
                    { "data", context }
                };
                pb.addJson(contextEnvelope, b64, "cx", "co");
            }
            pb.addDict(standardNvPairs);

            // TODO: remove debug code
            Console.WriteLine("about to display keys");
            foreach (string key in pb.NvPairs.Keys)
            {
                Console.WriteLine(key + ": " + pb.NvPairs[key]);
            }
            Console.WriteLine("finished displaying keys");

            track(pb);
        }

        public Tracker trackPageView(string pageUrl, string pageTitle = null, string referrer = null, Context context = null, Int64? tstamp = null)
        {
            Payload pb = new Payload();
            pb.add("e", "pv");
            pb.add("url", pageUrl);
            pb.add("page", pageTitle);
            pb.add("refr", referrer);
            completePayload(pb, context, tstamp);
            return this;
        }

        private void trackEcommerceTransactionItem(string orderId, string currency, TransactionItem item, Context context, Int64? tstamp)
        {
            Payload pb = new Payload();
            pb.add("e", "ti");
            pb.add("ti_id", orderId);
            pb.add("ti_cu", currency);
            pb.add("ti_sk", item.sku);
            pb.add("ti_pr", item.price);
            pb.add("ti_qu", item.quantity);
            pb.add("ti_nm", item.name);
            pb.add("ti_ca", item.category);
            completePayload(pb, context, tstamp);
        }

        public Tracker trackEcommerceTransaction(string orderId, double totalValue, string affiliation = null, double? taxValue = null, double? shipping = null, string city = null, string state = null, string country = null, string currency = null, List<TransactionItem> items = null, Context context = null, Int64? tstamp = null)
        {
            Payload pb = new Payload();
            pb.add("e", "tr");
            pb.add("tr_id", orderId);
            pb.add("tr_tt", totalValue);
            pb.add("tr_af", affiliation);
            pb.add("tr_tx", taxValue);
            pb.add("tr_sh", shipping);
            pb.add("tr_ci", city);
            pb.add("tr_st", state);
            pb.add("tr_co", country);
            pb.add("tr_cu", currency);
            completePayload(pb, context, tstamp);

            if (items != null)
            {
                foreach (TransactionItem item in items)
                {
                    trackEcommerceTransactionItem(orderId, currency, item, context, tstamp);
                }
            }

            return this;
        }

        public Tracker trackStructEvent(string category, string action, string label = null, string property = null, double? value = null, Context context = null, Int64? tstamp = null)
        {
            Payload pb = new Payload();
            pb.add("e", "se");
            pb.add("se_ca", category);
            pb.add("se_ac", action);
            pb.add("se_la", label);
            pb.add("se_pr", property);
            pb.add("se_va", value);
            completePayload(pb, context, tstamp);
            return this;
        }

        public Tracker trackUnstructEvent(SelfDescribingJson eventJson, Context context = null, Int64? tstamp = null)
        {
            var envelope = new Dictionary<string, object>
            {
                { "schema", "iglu:com.snowplowanalytics.snowplow/unstruct_event/jsonschema/1-0-0" },
                { "data", eventJson }
            };
            Payload pb = new Payload();
            pb.add("e", "ue");
            pb.addJson(envelope, b64, "ue_px", "ue_pr");
            completePayload(pb, context, tstamp);
            return this;
        }

        public Tracker trackScreenView(string name = null, string id = null, Context context = null, Int64? tstamp = null)
        {
            var screenViewProperties = new Dictionary<string, string>();
            if (name != null)
            {
                screenViewProperties["name"] = name;
            }
            if (id != null)
            {
                screenViewProperties["id"] = id;
            }
            var envelope = new Dictionary<string, object>
            {
                { "schema", "iglu:com.snowplowanalytics.snowplow/screen_view/jsonschema/1-0-0" },
                { "data", screenViewProperties }
            };
            trackUnstructEvent(envelope, context, tstamp);
            return this;
        }
    }
}