<script >
  import DropFile from '@svelte-parts/drop-file'
  export let showModal,jobId,jwtoken;
  const uploadFiles = (files) => {
console.log("Uploading file...");
const API_ENDPOINT = "https://localhost:7242/api/upload";
const request = new XMLHttpRequest();
const formData = new FormData();

request.open("POST", API_ENDPOINT, true);
request.setRequestHeader('Authorization', 'Bearer ' + jwtoken);
request.onreadystatechange = () => {
  if (request.readyState === 4 && request.status === 200) {
    jobId=request.responseText.replace(/['"]+/g, '');
  }
};

for (let i = 0; i < files.length; i++) {
  formData.append(files[i].name, files[i])
}
request.send(formData);

showModal=false;
};
</script>

<DropFile  onDrop={uploadFiles}> </DropFile>
<style>

</style>