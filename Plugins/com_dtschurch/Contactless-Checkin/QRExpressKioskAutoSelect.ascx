<%@ Control Language="C#" AutoEventWireup="true" CodeFile="QRExpressKioskAutoSelect.ascx.cs" Inherits="RockWeb.Plugins.com_DTS.ContactlessCheckin.QRExpressKioskAutoSelect" %>
<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <div class="checkin-search-actions checkin-start">

            <a class="btn btn-primary btn-checkin js-start-button" href="~/page/1529?KioskId=71&CheckinConfigId=14&GroupTypeIds=67%2C164%2C68%2C66%2C69%2C127%2C22&FamilyId={{PageParameter.FamilyId}}">
                  <span>Check-in</span>
               </a>
            
            </div>
                <div class="checkin-search-actions checkin-start">
            You should receive a text message with your Check-in QR code to the SMS enabled phone on your profile.  If you did not receive a text please come see the check-in counter at any kids check-in location.  <br /><br />
            <small><em>**Pre Check-in does not save your spot in a room and is first come first serve.</em></small>

        </div>

    </ContentTemplate>
</asp:UpdatePanel>
