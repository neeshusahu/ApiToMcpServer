import { useParams } from "react-router-dom";
import React from "react";  
import { Button, Card, CardContent, Typography } from "@mui/material";
import { useAuth } from "@workos-inc/authkit-react";
import { getClaims } from "@workos-inc/authkit-react";



function ApprovalPage() {
  const { id } = useParams();
  const { getAccessToken, signIn } = useAuth();
  const [disabled, setDisabled] = React.useState(false);
  const [status, setStatus] = React.useState(null);
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

console.log({
  sub: claims.sub,
  email: claims.email,
  firstName: claims.first_name,
  lastName: claims.last_name,
  name: claims.name
});
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
  setStatus(response.ok ? status : null);
  if (response.ok) {
    setDisabled(true);
  }
  else {
    setDisabled(false);
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
      {status && (
        <Typography sx={{ mt: 2 }}>
          Current Status: {status === "2" ? "Approved" : "Rejected"}
        </Typography>
      )}
    </Card>
  );
}

export default ApprovalPage;


