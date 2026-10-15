import { useParams } from "react-router-dom";
import React from "react";  
import { Button, Card, CardContent, Typography } from "@mui/material";
import { useAuth } from "@workos-inc/authkit-react";
import { getClaims } from "@workos-inc/authkit-react";



function ApprovalPage() {
  const { id } = useParams();
  const { getAccessToken, signIn, signOut } = useAuth();
  const [disabled, setDisabled] = React.useState(false);
  const [status, setStatus] = React.useState(null);
  const [msg, setMsg] = React.useState(null);
  const updateStatus = async (status) => {
    let accessToken;
    try {
      accessToken = await getAccessToken();
    } catch {
      // Session expired: sign in again and come back to this approval
      signIn({ state: { returnTo: window.location.pathname } });
      return;
    }
    const claims = getClaims(accessToken);
     for(const [key, value] of Object.entries(claims)) {
      console.log(`${key}: ${value}`);
    }
    console.log(`Updating status to ${status} for approval ID ${id}`);
  const response = await fetch(
    `http://localhost:5282/approval/${id}?approvalStatus=${status}`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    }
  );
  // 401 = stale sign-in (reauth_required) or bad token: sign out; RequireAuth then forces a fresh sign-in
  // and brings the user back to this approval
  if (response.status === 401) {
    sessionStorage.setItem("pendingApproval", window.location.pathname);
    signOut({ returnTo: window.location.origin });
    return;
  }
  setStatus(response.ok ? status : null);
if (response.ok || [403, 404, 409, 410].includes(response.status)) {
    setDisabled(true);
    setMsg(response.status === 403 ? "Only the person who requested this action can approve it." :
      response.status === 404 ? "Approval not found." :
      response.status === 410 ? "Approval has expired." :
      response.status === 409 ? "Approval is already processed." :
      null);
  }
  else {
    setDisabled(false);
   setMsg(`Something went wrong (${response.status}). Please try again.`);
  }
};
  return (
       <Card sx={{ maxWidth: 500, margin: "100px auto" }}>
      <CardContent>
        <Typography variant="h5">
          Approval Required
        </Typography>

        <Typography sx={{ mt: 2 }}>
          Approval ID: {id}
        </Typography>
        <Button
          variant="contained"
          color="primary"
          sx={{ mt: 2 }}
          onClick={() => updateStatus("2")}
          disabled={disabled}
        >
          Approve
        </Button>
        <Button variant="outlined" 
        color="secondary" sx={{ mt: 2, ml: 2 }} 
        onClick={() => updateStatus("3")}
        disabled={disabled}>
          Reject
        </Button>
      </CardContent>
      {msg!=null && (
        <Typography sx={{ mt: 2 }}>
          {msg}
        </Typography>
      )}
    </Card>
  );
}

export default ApprovalPage;


