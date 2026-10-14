import React from 'react';
import {useAuth} from '@workos-inc/authkit-react';
import {useEffect} from 'react';

const Login = () => {
  const {signIn} = useAuth();

  useEffect(() => {
    console.log('Login component ');
    console.log('Sign in function: ', signIn);
    signIn();
    // You can add any side effects related to login here if needed
  }, [signIn]);
  
  return (
    <div>Redirecting to login...</div>
  );
};

export default Login;