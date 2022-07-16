<script>
import UploadFiles from "./components/UploadFiles.svelte"
import ProgressBar from "./components/ProgressBar.svelte"
import GoogleSignIn from "./components/GoogleSignIn.svelte"
let showModal = true,showStop=true,stop=false,jobId="", userLoggedIn = false,jwtoken="";
function download(dataurl) {
  const link = document.createElement("a");
  link.href = dataurl;
  link.click();
}
 
 
</script>

<main>
	<div class="container">
    {#if !userLoggedIn}
    <GoogleSignIn bind:userLoggedIn bind:jwtoken />
      {:else}
      {#if showModal}
      <div  class=uploadZone>
        <UploadFiles bind:jwtoken bind:jobId bind:showModal></UploadFiles>
      </div>
        
      {:else }
      <ProgressBar {jobId} {stop} bind:showStop />
      <div class="step-button">
        {#if showStop}
        <button class="btn" on:click={ () => {fetch("https://localhost:7242/api/abort/"+jobId,{ method: "POST"}); stop=true; showStop=true;} }>Stop</button>
        {:else}
        <button class="btn" on:click={()=>{download("https://localhost:7242/api/download/"+jobId); stop=true;}} disabled={stop} >Download</button>
        <button class="btn" on:click={() => {stop=false;showModal=true;showStop=true;}} >New</button>
        {/if}
        
      </div>	
      {/if}	
    {/if}
	</div>	  
</main>

<style>
@import url('https://fonts.googleapis.com/css?family=Muli&display=swap');

* {
  box-sizing: border-box;
}

main {
  font-family: 'Muli', sans-serif;
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100vh;
  overflow: hidden;
  margin: 0;
}
.uploadZone{
  width: 30vw;
}
.btn {
  background-color: #3498db;
  color: #fff;
  border: 0;
  border-radius: 6px;
  cursor: pointer;
  font-family: inherit;
  padding: 8px 30px;
  margin: 5px;
  font-size: 14px;
}

.btn:active {
  transform: scale(0.98);
}

.btn:focus {
  outline: 0;
}

.btn:disabled {
  background-color: #e0e0e0;
  cursor: not-allowed;
}

.step-button{
  margin-top: 1rem;
  text-align: center;
}
</style>