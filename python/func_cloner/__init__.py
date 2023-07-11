import datetime
import logging
import os
import time
import git

import azure.functions as func


def main(mytimer: func.TimerRequest) -> None:
    utc_timestamp = datetime.datetime.utcnow().replace(
        tzinfo=datetime.timezone.utc).isoformat()

    if mytimer.past_due:
        logging.info('The timer is past due!')

    logging.info('Python timer trigger function ran at %s', utc_timestamp)
    repository_url = os.getenv("RepositoryUrl")
    target_folder = os.getenv("TargetFolder")
    delete_threshold = int(os.getenv("DirectoryDeleteThreshold", 300))
    if repository_url is None :
        logging.error("Setting is null:repository_url")
    elif target_folder is None:
        logging.error("Setting is null:target_folder")
    else:
        destination_path = create_folder_with_epoch_timestamp(target_folder)
        logging.info(f"Begin cloning at:{destination_path}")
        clone_file(repository_url, destination_path)
        delete_old_folders(target_folder, delete_threshold)

#This function creates a folder with epoch timestamp
def create_folder_with_epoch_timestamp(file_path):
    timestamp = int(time.time())
    folder_name = str(timestamp)
    folder_path = os.path.join(file_path, folder_name)
    os.makedirs(folder_path)
    return folder_path

#This function clones the wiki to a destination location
def clone_file(repository_url, destination_path):
    start_time = time.time()
    try:
        # Clone the repository using the provided URL and PAT token
        repo = git.Repo.clone_from(repository_url, destination_path, env={"config": "core.longpaths=true"})
    except git.GitCommandError as e:
        logging.error( str(e))
    except Exception as e:
        logging.error( f"An error occurred: {str(e)}")
    elapsed_time = time.time() - start_time
    logging.info("Time taken: %s seconds", elapsed_time)

#This function deletes folder beyond a threshold
def delete_old_folders(target_folder: str, time_in_minutes: int) -> None:
    current_time = datetime.datetime.now()
    time_threshold = current_time - datetime.timedelta(minutes=time_in_minutes)
    folders = [folder for folder in os.listdir(target_folder) if os.path.isdir(os.path.join(target_folder, folder))]
    # Iterate over the folders
    for folder in folders:
        folder_path = os.path.join(target_folder, folder)
        # Get the folder's modification time
        folder_time = datetime.datetime.fromtimestamp(os.path.getmtime(folder_path))
        # Compare the folder's time with the threshold
        if folder_time < time_threshold:
            # Delete the folder and its contents
            for root, dirs, files in os.walk(folder_path, topdown=False):
                for file in files:
                    file_path = os.path.join(root, file)
                    os.remove(file_path)
                for dir_ in dirs:
                    dir_path = os.path.join(root, dir_)
                    os.rmdir(dir_path)
            os.rmdir(folder_path)