import React from "react";
import { useLocation } from "react-router-dom";
import { getClaims, useAuth } from "@workos-inc/authkit-react";
import { Typography } from "@mui/material";

// Signs the user in before rendering the route, then brings them back to the same URL
function RequireAuth({ children }) {
  const { user, isLoading, getAccessToken, signIn, signOut } = useAuth();
  const location = useLocation();
  //Forcing fresh sign in for approval link
  const MAX_AUTH_AGE=300;
  const [freshSignIn, setFreshSignIn] = React.useState(true);

  React.useEffect(() => {
    if(isLoading) return;
    if (!user) {
      signIn({ state: { returnTo: location.pathname + location.search } });
      return;
    }
    getAccessToken().then(token => {
      const authTime=getClaims(token).auth_time;
      const age=Date.now()/1000-authTime;
      if(authTime && age <MAX_AUTH_AGE) {
        setFreshSignIn(false);
      }
      else {
        setFreshSignIn(true);
        // Sign-out can only return to the allowed redirect (the origin); Home resumes this approval
        sessionStorage.setItem("pendingApproval", location.pathname + location.search);
        signOut({ returnTo: window.location.origin });
      }
      // Do something with the access token
    });
  }, [isLoading, user, signIn, signOut, location]);

  

  return freshSignIn ? <Typography sx={{ m: 4 }}>Signing in...</Typography> : children;
}

export default RequireAuth;
