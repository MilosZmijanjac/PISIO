  <head>
        <script src="https://accounts.google.com/gsi/client" async defer></script>
  </head>

      
      <script>
        export let userLoggedIn=false,jwtoken;
        function handleCredentialResponse(response) {
          userLoggedIn=true;
          jwtoken=response.credential
        }
        window.onload = function () {
          google.accounts.id.initialize({
            client_id: "172129971249-fd81dalc90ll0csqnqnj62rtjotibl10.apps.googleusercontent.com",
            callback: handleCredentialResponse
          });
          google.accounts.id.renderButton(
            document.getElementById("buttonDiv"),
            { theme: "outline", size: "large" }  // customization attributes
          );
          google.accounts.id.prompt(); // also display the One Tap dialog
        }
    </script>
    <div id="buttonDiv"></div> 
