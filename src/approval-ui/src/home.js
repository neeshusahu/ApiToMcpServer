import React from "react";
import { useAuth } from "@workos-inc/authkit-react";
import { Button, Card, CardContent, Typography } from "@mui/material";

function Home() {
  const { user, isLoading, signIn, signOut } = useAuth();

  if (isLoading) {
    return <Typography sx={{ m: 4 }}>Loading...</Typography>;
  }

  return (
    <Card sx={{ maxWidth: 500, margin: "100px auto" }}>
      <CardContent>
        <Typography variant="h5">Approvals</Typography>
        {user ? (
          <>
            <Typography sx={{ mt: 2 }}>Signed in as {user.id}</Typography>
            <Button variant="outlined" sx={{ mt: 2 }} onClick={() => signOut()}>
              Sign out
            </Button>
          </>
        ) : (
          <Button variant="contained" sx={{ mt: 2 }} onClick={() => signIn()}>
            Sign in
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

export default Home;
