﻿// <copyright>
// Copyright by the Divine Technology Systems
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.HtmlControls;

using Rock;
using Rock.Attribute;
using Rock.CheckIn;
using Rock.Data;
using Rock.Model;
using Rock.Utility;
using Rock.Web.UI;

namespace RockWeb.Plugins.com_DTS.ContactlessCheckin
{
    /// <summary>
    /// 
    /// </summary>
    [DisplayName( "QR Express Success" )]
    [Category( "com_DTS > Check-in" )]
    [Description( "Displays the details of a successful checkin with added attribute for disclaimer message." )]

    [LinkedPage( "Person Select Page", "", false, "", "", 5 )]
    [TextField( "Title", "", false, "Checked-in", "Text", 6 )]
    [TextField( "Detail Message", "The message to display indicating person has been checked in. Use {0} for person, {1} for group, {2} for schedule, and {3} for the security code", false,
        "{0} was checked into {1} in {2} at {3}", "Text", 7 )]
    [CodeEditorField("Disclaimer Message",
        Key = "DisclaimerMessage",
        Description = "The message to display at the bottom of the screen indicating the room is first come first serve and the QR code has been sent to SMS enabled numbers",
       IsRequired = true,
       DefaultValue = @"<style>
        .checkin-start {
            margin-top: 50% !important;
        }
        </style>

        <div class='checkin-search-actions checkin-start'>
            <span>You should receive a text message with your Check-in QR code to the SMS enabled phone on your profile.  If you did not receive a text please come see the check-in counter at any kids check-in location.  <br /><br />
            <small><em>**Pre Check-in does not save your spot in a room and is first come first serve.</em></small></span>
        </div>"
        )]
        [TextField("QR Code Recipient Message", "The message to display for each adult in the family that will receive a QR code.", false, "{0} will received a QR code", "", 0, "QRCodeRecipientMessage")]
    [BooleanField("Enabled SMS Numbers Only", "", true, "", 0, "EnabledSMSNumbersOnly")]
    public partial class QRExpressSuccess : CheckInBlock
    {
        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            RockPage.AddScriptLink( "~/Scripts/CheckinClient/ZebraPrint.js" );
            RockPage.AddScriptLink( "~/Scripts/CheckinClient/checkin-core.js" );

            var bodyTag = this.Page.Master.FindControl( "bodyTag" ) as HtmlGenericControl;
            if ( bodyTag != null )
            {
                bodyTag.AddCssClass( "checkin-success-bg" );
            }
        }

        /// <summary>
        /// CheckinResult for rendering the Success Lava Template
        /// </summary>
        /// <seealso cref="DotLiquid.Drop" />
        public class CheckinResult : DotLiquid.Drop
        {
            /// <summary>
            /// Gets the person.
            /// </summary>
            /// <value>
            /// The person.
            /// </value>
            public CheckInPerson Person { get; internal set; }

            /// <summary>
            /// Gets the group.
            /// </summary>
            /// <value>
            /// The group.
            /// </value>
            public CheckInGroup Group { get; internal set; }

            /// <summary>
            /// Gets the location.
            /// </summary>
            /// <value>
            /// The location.
            /// </value>
            public Location Location { get; internal set; }

            /// <summary>
            /// Gets the schedule.
            /// </summary>
            /// <value>
            /// The schedule.
            /// </value>
            public CheckInSchedule Schedule { get; internal set; }

            /// <summary>
            /// Gets the detail message.
            /// </summary>
            /// <value>
            /// The detail message.
            /// </value>
            public string DetailMessage { get; internal set; }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( CurrentWorkflow == null || CurrentCheckInState == null )
            {
                NavigateToHomePage();
            }
            else
            {
                if ( !Page.IsPostBack )
                {
                    try
                    {
                        lTitle.Text = GetAttributeValue( "Title" );
                        string detailMsg = GetAttributeValue( "DetailMessage" );
                        Guid adultGuid = Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid();


                        var printFromClient = new List<CheckInLabel>();
                        var printFromServer = new List<CheckInLabel>();

                        List<CheckinResult> checkinResultList = new List<CheckinResult>();

                        // Print the labels
                        foreach ( var family in CurrentCheckInState.CheckIn.Families.Where( f => f.Selected ) )
                        {
                            lbAnother.Visible =
                                CurrentCheckInState.CheckInType.TypeOfCheckin == TypeOfCheckin.Individual &&
                                family.People.Count > 1;

                            foreach ( var person in family.GetPeople( true ) )
                            {
                                foreach ( var groupType in person.GetGroupTypes( true ) )
                                {
                                    foreach ( var group in groupType.GetGroups( true ) )
                                    {
                                        foreach ( var location in group.GetLocations( true ) )
                                        {
                                            foreach ( var schedule in location.GetSchedules( true ) )
                                            {
                                                string detailMessage = string.Format( detailMsg, person.ToString(), group.ToString(), location.Location.Name, schedule.ToString(), person.SecurityCode );
                                                CheckinResult checkinResult = new CheckinResult();
                                                checkinResult.Person = person;
                                                checkinResult.Group = group;
                                                checkinResult.Location = location.Location;
                                                checkinResult.Schedule = schedule;
                                                checkinResult.DetailMessage = detailMessage;
                                                checkinResultList.Add( checkinResult );
                                            }
                                        }
                                    }

                                    if ( groupType.Labels != null && groupType.Labels.Any() )
                                    {
                                        printFromClient.AddRange( groupType.Labels.Where( l => l.PrintFrom == Rock.Model.PrintFrom.Client ) );
                                        printFromServer.AddRange( groupType.Labels.Where( l => l.PrintFrom == Rock.Model.PrintFrom.Server ) );
                                    }
                                }
                            }

                            /// QR code logic for who receives the text message
                            using (var rockContext = new RockContext())
                            {

                                var attendanceRecords = new AttendanceService(rockContext).Queryable().Where(a => family.AttendanceIds.Contains(a.Id)).GroupBy(a => a.ForeignKey);

                                foreach (IGrouping<string, Attendance> grouping in attendanceRecords)
                                {

                                    
                                    List<GroupMember> peopleInFamily = grouping.ToList().First().SearchResultGroup.ActiveMembers().Where(gm => gm.Person.PhoneNumbers.Count > 0).Where(gm => gm.GroupRole.Guid == adultGuid).ToList();

                                    foreach (GroupMember personInFamily in peopleInFamily)
                                    {

                                        int? personAliasId = personInFamily.Person.Aliases.FirstOrDefault().Id;

                                        if (personAliasId != null)
                                        {
                                            int phoneNumber;
                                            var phoneNumberQry = new PersonAliasService(rockContext).Queryable()
                                                .Where(a => a.Id.Equals(personAliasId.Value))
                                                .SelectMany(a => a.Person.PhoneNumbers);


                                            if (GetAttributeValue("EnabledSMSNumbersOnly").AsBoolean())
                                            {
                                                phoneNumber = phoneNumberQry.Where(p => p.IsMessagingEnabled).Count();

                                            }
                                            else
                                            {
                                                phoneNumber = phoneNumberQry.Count();
                                            }

                                            if (phoneNumber > 0)
                                            {
                                                // add qr code sent to phone number sms message
                                                string qrCodeRecipientMsg = GetAttributeValue("QRCodeRecipientMessage");
                                                string qrCodeRecipientMessage = string.Format(qrCodeRecipientMsg, personInFamily.Person);
                                                CheckinResult checkinResult = new CheckinResult();
                                                checkinResult.DetailMessage = qrCodeRecipientMessage;
                                                checkinResultList.Add(checkinResult);
                                            }
                                        }
                                    }
                                }
                            }
                            ///

                        }


                        


                        var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( this.RockPage, null, new Rock.Lava.CommonMergeFieldsOptions { GetLegacyGlobalMergeFields = false } );
                        mergeFields.Add( "CheckinResultList", checkinResultList );
                        mergeFields.Add( "Kiosk", CurrentCheckInState.Kiosk );
                        mergeFields.Add( "RegistrationModeEnabled", CurrentCheckInState.Kiosk.RegistrationModeEnabled );
                        mergeFields.Add( "Messages", CurrentCheckInState.Messages );
                        if ( CurrentGroupTypeIds != null )
                        {
                            var checkInAreas = CurrentGroupTypeIds.Select( a => Rock.Web.Cache.GroupTypeCache.Get( a ) );
                            mergeFields.Add( "CheckinAreas", checkInAreas );
                        }

                        if ( printFromClient.Any() )
                        {
                            // When debugging and using ngrok you will need to change this to the ngrok address (e.g. var urlRoot = "http://developrock.ngrok.io";). Not sure why this isn't using a global attribute.
                            var urlRoot = string.Format( "{0}://{1}", Request.Url.Scheme, Request.Url.Authority );
                            printFromClient
                                .OrderBy( l => l.PersonId )
                                .ThenBy( l => l.Order )
                                .ToList()
                                .ForEach( l => l.LabelFile = urlRoot + l.LabelFile );
                            AddLabelScript( printFromClient.ToJson() );
                        }

                        if ( printFromServer.Any() )
                        {
                            var messages = ZebraPrint.PrintLabels( printFromServer );
                            mergeFields.Add( "ZebraPrintMessageList", messages );
                        }

                        var successLavaTemplate = CurrentCheckInState.CheckInType.SuccessLavaTemplate;
                        lCheckinResultsHtml.Text = successLavaTemplate.ResolveMergeFields( mergeFields );
                        lCheckinResultsHtml.Text += GetAttributeValue("DisclaimerMessage");

                    }
                    catch ( Exception ex )
                    {
                        LogException( ex );
                    }
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the lbDone control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void lbDone_Click( object sender, EventArgs e )
        {
            NavigateToHomePage();
        }

        /// <summary>
        /// Handles the Click event of the lbAnother control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void lbAnother_Click( object sender, EventArgs e )
        {
            if ( KioskCurrentlyActive )
            {
                foreach ( var family in CurrentCheckInState.CheckIn.Families.Where( f => f.Selected ) )
                {
                    foreach ( var person in family.People.Where( p => p.Selected ) )
                    {
                        person.Selected = false;

                        foreach ( var groupType in person.GroupTypes.Where( g => g.Selected ) )
                        {
                            groupType.Selected = false;
                        }
                    }
                }

                SaveState();
                NavigateToLinkedPage( "PersonSelectPage" );

            }
            else
            {
                NavigateToHomePage();
            }
        }

        /// <summary>
        /// Adds the label script.
        /// </summary>
        /// <param name="jsonObject">The json object.</param>
        private void AddLabelScript( string jsonObject )
        {
            string script = string.Format( @"

        // setup deviceready event to wait for cordova
	    if (navigator.userAgent.match(/(iPhone|iPod|iPad)/) && typeof window.RockCheckinNative === 'undefined') {{
            document.addEventListener('deviceready', onDeviceReady, false);
        }} else {{
            $( document ).ready(function() {{
                onDeviceReady();
            }});
        }}

	    // label data
        var labelData = {0};

		function onDeviceReady() {{
            try {{			
                printLabels();
            }} 
            catch (err) {{
                console.log('An error occurred printing labels: ' + err);
            }}
		}}
		
		function printLabels() {{
		    ZebraPrintPlugin.printTags(
            	JSON.stringify(labelData), 
            	function(result) {{ 
			        console.log('Tag printed');
			    }},
			    function(error) {{   
				    // error is an array where:
				    // error[0] is the error message
				    // error[1] determines if a re-print is possible (in the case where the JSON is good, but the printer was not connected)
			        console.log('An error occurred: ' + error[0]);
                    alert('An error occurred while printing the labels. ' + error[0]);
			    }}
            );
	    }}
", jsonObject );
            ScriptManager.RegisterStartupScript( this, this.GetType(), "addLabelScript", script, true );
        }

    }
}