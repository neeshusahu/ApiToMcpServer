import React from "react";
import { useLocation } from "react-router-dom";
import { useAuth } from "@workos-inc/authkit-react";
import { Typography } from "@mui/material";

// Signs the user in before rendering the route, then brings them back to the same URL
function RequireAuth({ children }) {
  const { user, isLoading, signIn } = useAuth();
  const location = useLocation();

  React.useEffect(() => {
    if (!isLoading && !user) {
      signIn({ state: { returnTo: location.pathname + location.search } });
    }
  }, [isLoading, user, signIn, location]);

  if (isLoading || !user) {
    return <Typography sx={{ m: 4 }}>Redirecting to sign in...</Typography>;
  }

  return children;
}

export default RequireAuth;
