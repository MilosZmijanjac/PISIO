<script>
	export let steps = ['Upload', 'OCR', 'GIF', 'PDF','Complete'], currentActive = 1;
	let circles, progress;
  	export let status="",newStatus="",showStop=false,stop=false,jobId; 
		console.log(jobId)
	 const handleProgress = (stepIncrement) => {
		circles = document.querySelectorAll('.circle');
		if(stepIncrement == 1){
			currentActive++

			if(currentActive > circles.length) {
					currentActive = circles.length
			}
		} else {
			currentActive--

			if(currentActive < 1) {
					currentActive = 1 
			}
		}
		

    update()
}
	
	function update() {
    circles.forEach((circle, idx) => {
        if(idx < currentActive) {
            circle.classList.add('active')
        } else {
            circle.classList.remove('active')
        }
    })

    const actives = document.querySelectorAll('.active');


    progress.style.width = (actives.length - 1) / (circles.length - 1) * 100 + '%';
	}

	var refreshIntervalID=setInterval(() => {
		if(jobId==="")
		return;
        checkStatus().then((result)=>newStatus=result);
				if(stop)
        {console.log("lol");
				 showStop=false;
				clearInterval(refreshIntervalID); 
			 }
        if(currentActive==steps.length){
					console.log(status+"---"+newStatus)
          showStop=false;
          clearInterval(refreshIntervalID);
        }
        if(status!==newStatus){
					console.log(currentActive)
          handleProgress(+1);
          status=newStatus;
        }
		
      }
  , 100);

	const checkStatus = async () => {
  const response = await fetch("https://localhost:7242/api/status/".concat(jobId));
  return  response.text(); 
};
	
</script>

<div class="progress-container" bind:this={circles}>
	<div class="progress" bind:this={progress}></div>
	{#each steps as step, i}
	<div class="circle {i == 0 ? 'active' : ''}" data-title={step} >{i+1}</div>
	{/each}
</div>


<style>
	.progress-container {
		display: flex;
		justify-content: space-between;
		position: relative;
		margin-bottom: 30px;
		max-width: 100%;
		width: 350px;
	}

	.progress-container::before {
		content: '';
		background-color: #e0e0e0;
		position: absolute;
		top: 50%;
		left: 0;
		transform: translateY(-50%);
		height: 4px;
		width: 100%;
		z-index: -1;
	}

	.progress {
		background-color: #3498db;
		position: absolute;
		top: 50%;
		left: 0;
		transform: translateY(-50%);
		height: 4px;
		width: 0%;
		z-index: -1;
		transition: 0.8s ease;
		
	}

	.circle {
		background-color: #fff;
		color: #999;
		border-radius: 50%;
		height: 30px;
		width: 30px;
		display: flex;
		align-items: center;
		justify-content: center;
		border: 3px solid #e0e0e0;
		transition: 0.8s ease;
		cursor: pointer;
	}
	
	.circle::after{
		content: attr(data-title) " ";
		position: absolute;
		bottom: 35px;
		color: #999;
		transition: 0.4s ease;
		
	}
	
	.circle.active::after {
		color: #3498db;
	}

	.circle.active {
		border-color: #3498db;	
		animation: pulse 2s infinite;	
	}

	@keyframes pulse {
	0% {
		transform: scale(0.95);
		box-shadow: 0 0 0 0 rgba(0, 0, 0, 0.7);
	}

	70% {
		transform: scale(1);
		box-shadow: 0 0 0 10px rgba(0, 0, 0, 0);
	}

	100% {
		transform: scale(0.95);
		box-shadow: 0 0 0 0 rgba(0, 0, 0, 0);
	}
}
</style>