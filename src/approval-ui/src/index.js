import React from 'react';
import ReactDOM from 'react-dom/client';
import {AuthKitProvider} from '@workos-inc/authkit-react';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  <React.StrictMode>
    <AuthKitProvider
      clientId={process.env.REACT_APP_WORKOS_CLIENT_ID}
      onRedirectCallback={({ state }) => {
        // Return to the page that started sign-in, e.g. /approval/:id
        if (state?.returnTo) window.location.replace(state.returnTo);
      }}
    >
      <App />
    </AuthKitProvider>
  </React.StrictMode>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
